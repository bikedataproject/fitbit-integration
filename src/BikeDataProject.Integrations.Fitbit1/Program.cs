using System;
using System.IO;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;
using Microsoft.Extensions.Configuration;

namespace BikeDataProject.Integrations.Fitbit
{
    class Program
    {
        static void Main(string[] args)
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
            
            var fitbitCredentials = new FitbitAppCredentials()
            {
                ClientId = configuration["FITBIT_CLIENT_ID"],
                ClientSecret = File.ReadAllText(configuration["FITBIT_CLIENT_SECRET"]);
            };
            
            var client = new FitbitClient(fitbitCredentials, )
        }
    }
}
