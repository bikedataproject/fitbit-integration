using System;
using System.IO;
using System.Threading.Tasks;
using BikeDataProject.DB;
using BikeDataProject.Integrations.Fitbit.Db;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Formatting.Json;

namespace BikeDataProject.Integrations.FitBit.Subscriber
{
    class Program
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

                // get deploy time setting.
                var (deployTimeSettings, envVarPrefix) = configurationBuilder.GetDeployTimeSettings();

                try
                {
                    var host = WebHost.CreateDefaultBuilder(args)
                        .ConfigureAppConfiguration((hostingContext, config) =>
                        {
                            Log.Information($"Env: {hostingContext.HostingEnvironment.EnvironmentName}");
                            
                            config.AddJsonFile(deployTimeSettings, true, true);
                            config.AddEnvironmentVariables((c) => { c.Prefix = envVarPrefix; });
                        }).ConfigureServices((hostingContext, services) =>
                        {
                            services.AddDbContext<FitbitDbContext>(o => o.UseNpgsql(
                                    File.ReadAllText(hostingContext.Configuration["FITBIT_DB"])),
                                ServiceLifetime.Singleton);
                            
                            services.AddDbContext<BikeDataDbContext>(o => o.UseNpgsql(
                                    File.ReadAllText(hostingContext.Configuration["DB"])),
                                ServiceLifetime.Singleton);
                            
                            services.AddHostedService<Worker>();
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
