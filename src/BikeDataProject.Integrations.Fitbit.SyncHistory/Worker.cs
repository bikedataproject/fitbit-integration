using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.DB.Domain;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using Fitbit.Api.Portable.OAuth2;
using Fitbit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using User = BikeDataProject.DB.Domain.User;

namespace BikeDataProject.Integrations.Fitbit.SyncHistory
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly FitbitDbContext _db;
        private readonly BikeDataDbContext _contributionsDb;
        private readonly HashSet<int> _activityTypes = new ();

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            FitbitDbContext db, BikeDataDbContext contributionsDb)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
            _contributionsDb = contributionsDb;
        }

        private DateTime _lastActivityTypeSync = DateTime.Now;
        
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
                var accessToken = new OAuth2AccessToken()
                {
                    Scope = user.Scope,
                    ExpiresIn = user.ExpiresIn,
                    RefreshToken = user.RefreshToken,
                    Token = user.Token,
                    TokenType = user.TokenType,
                    UserId = user.UserId
                };
                var fitbitClient = new FitbitClient(fitbitAppCredentials, accessToken);
                
                // refresh token if needed.
                if (!accessToken.IsFresh(user.TokenCreated))
                {
                    // refresh token.
                    accessToken = await fitbitClient.RefreshOAuth2TokenAsync();

                    // update details.
                    user.Scope = accessToken.Scope;
                    user.Token = accessToken.Token;
                    user.ExpiresIn = accessToken.ExpiresIn;
                    user.RefreshToken = accessToken.RefreshToken;
                    user.TokenType = accessToken.TokenType;
                    user.TokenCreated = DateTime.UtcNow;
                    _db.Users.Update(user);
                    // ReSharper disable once MethodSupportsCancellation
                    await _db.SaveChangesAsync();
                }

                // get activity types if needed.
                // make sure to refresh once in a while.
                if ((DateTime.Now - _lastActivityTypeSync).TotalHours > 2)
                {
                    _activityTypes.Clear();
                }
                if (_activityTypes.Count == 0)
                {
                    // get activity types.
                    var types = await fitbitClient.GetActivityCategoryListAsync();
                    
                    // the activity id with name 'Bicycling'.
                    foreach (var category in types.Categories)
                    {
                        if (category.Name == "Bicycling")
                        {
                            foreach (var activity in category.Activities)
                            {
                                _activityTypes.Add(activity.Id);
                            }
                        }

                        if (category.SubCategories == null) continue;
                        
                        foreach (var subCategory in category.SubCategories)
                        {
                            if (subCategory.Name == "Bicycling")
                            {
                                foreach (var activity in subCategory.Activities)
                                {
                                    _activityTypes.Add(activity.Id);
                                }
                            }
                        }
                    }

                    if (_activityTypes.Count == 0)
                    {
                        _logger.LogCritical("Bicycling activity types not found, cannot synchronize activities without them.");
                    }
                    
                    _lastActivityTypeSync = DateTime.Now;
                }
                
                // get cycling activities.
                var after = user.LatestSyncedStamp ?? (new DateTime(1970, 1, 1)).ToUniversalTime();
                var activities = await fitbitClient.GetActivityLogsListAsync(null, after);
                if (activities?.Activities == null) return;

                User? contributionsDbUser = null;
                foreach (var activity in activities.Activities)
                {
                    // if not a cycling activity, ignore.
                    if (!_activityTypes.Contains(activity.ActivityTypeId)) continue;
                    
                    if (stoppingToken.IsCancellationRequested) break;
                    
                    // get tcx.
                    var tcx = await fitbitClient.GetApiFreeResponseAsync(activity.TcxLink);
                    var tcxParse = TCX.Parser.Parse(tcx); 
                    
                    // create user if needed.
                    contributionsDbUser ??= await _contributionsDb.CreateOrGetUser(user.UserId);
                    
                    
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unhandled exception.");
            }
        }
    }
}