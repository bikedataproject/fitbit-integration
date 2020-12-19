using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fitbit.Api.Portable;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;

namespace BikeDataProject.Integrations.Fitbit
{
    public class Program
    {
        public static async Task Main(string[] args)
        {           
            // hardcode configuration before the configured logging can be bootstrapped.
            var logFile = Path.Combine("logs", "boot-log-.txt");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(new JsonFormatter(), logFile, rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();
            
            try
            {
                var configurationBuilder = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", true, true);

                // get deploy time settings if present.
                var configuration = configurationBuilder.Build();
                var deployTimeSettings = configuration["deploy-time-settings"] ?? "/var/app/config/appsettings.json";
                configurationBuilder = configurationBuilder
                    .AddJsonFile(deployTimeSettings, true, true);

                // get environment variable prefix.
                configuration = configurationBuilder.Build();
                var envVarPrefix = configuration["env-var-prefix"] ?? "BIKEDATA_";
                configurationBuilder = configurationBuilder
                    .AddEnvironmentVariables((c) => { c.Prefix = envVarPrefix; });

                // build configuration.
                configuration = configurationBuilder.Build();

                // read/parse configurations.
                var fitbitCredentials = new FitbitAppCredentials()
                {
                    ClientId = configuration["FITBIT_CLIENT_ID"],
                    ClientSecret = await File.ReadAllTextAsync(configuration["FITBIT_CLIENT_SECRET"])
                };
                var subVerCode = await File.ReadAllTextAsync(configuration["FITBIT_SUB_VER_CODE"]);

                // setup logging.
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                try
                {
                    var host = WebHost.CreateDefaultBuilder(args)
                        .ConfigureServices((_, services) =>
                        {
                            // add logging.
                            services.AddLogging(b => { b.AddSerilog(); });

                            // add configuration.
                            services.AddSingleton(new StartupConfiguration()
                            {
                                FitbitAppCredentials = fitbitCredentials,
                                SubscriptionVerificationCode = subVerCode
                            });
                        }).UseStartup<Startup>().Build();

                    // run!
                    await host.RunAsync();
                }
                catch (Exception e)
                {
                    Log.Logger.Fatal(e, "Unhandled exception.");
                }
            }
            catch (Exception e)
            {
                Log.Logger.Fatal(e, "Unhandled exception.");
                throw;
            }
        }
    }
}