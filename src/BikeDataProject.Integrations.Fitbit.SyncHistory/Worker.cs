using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using Fitbit.Api.Portable.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.SyncHistory
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly FitbitDbContext _db;

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            FitbitDbContext db)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
        }

        private bool _hasActivityTypes = false;
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var refreshTime = _configuration.GetValueOrDefault<int>("refresh-time", 1000);
            
            // read/parse fitbit configurations.
            var fitbitCredentials = new FitbitAppCredentials()
            {
                ClientId = _configuration["FITBIT_CLIENT_ID"],
                ClientSecret = await File.ReadAllTextAsync(_configuration["FITBIT_CLIENT_SECRET"], stoppingToken)
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Worker running at: {time}, triggered every {refreshTime}", 
                    DateTimeOffset.Now, refreshTime);

                await this.RunAsync(fitbitCredentials, stoppingToken);
                
                await Task.Delay(refreshTime, stoppingToken);
            }
        }

        private async Task RunAsync(FitbitAppCredentials fitbitAppCredentials, CancellationToken stoppingToken)
        {
            try
            {
                var user = (from users in _db.Users
                    where users.AllSynced == false
                    select users).FirstOrDefault();
                
                // no user found without history unsynced.
                if (user == null) return;

                // try to get historic activities.
                var fitbitClient = new FitbitClient(fitbitAppCredentials, new OAuth2AccessToken()
                {
                    Scope = user.Scope,
                    ExpiresIn = user.ExpiresIn,
                    RefreshToken = user.RefreshToken,
                    Token = user.Token,
                    TokenType = user.TokenType,
                    UserId = user.UserId
                });
                DateTime? after = user.LatestSyncedStamp ?? (new DateTime(1970, 1, 1)).ToUniversalTime();

                // get activity types if needed.
                if (!_hasActivityTypes)
                {
                    // get activity types.
                    var types = fitbitClient.GetActivityTypeListAsync();
                    
                }

                // var activities = await fitbitClient.GetActivityLogsListAsync(null, after);
                // foreach (var activity in activities.Activities)
                // {
                //     ActivityLevel
                //     activity.ActivityTypeId
                // }
                //
                // activities.Activities
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unhandled exception.");
            }
        }
    }
}