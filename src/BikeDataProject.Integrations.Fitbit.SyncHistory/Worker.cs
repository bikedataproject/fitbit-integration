using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
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
        private readonly DB.BikeDataDbContext _contributionsDb;
        private readonly HashSet<int> _activityTypes = new ();

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            FitbitDbContext db, DB.BikeDataDbContext contributionsDb)
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

                DB.User? contributionsDbUser = null;
                foreach (var activity in activities.Activities)
                {
                    // if not a cycling activity, ignore.
                    if (!_activityTypes.Contains(activity.ActivityTypeId)) continue;
                    
                    if (stoppingToken.IsCancellationRequested) break;
                    
                    // check if activity with log id was already included.
                    if (_db.UserHasContributionWithLogId(user, activity.LogId)) continue;
                    
                    // get tcx.
                    var tcx = await fitbitClient.GetApiFreeResponseAsync(activity.TcxLink);
                    var tcxParsed = TCX.Parser.Parse(tcx); 
                    if (tcxParsed == null) continue;
                    
                    // create user in contributions db if needed and keep the id.
                    contributionsDbUser ??= await _contributionsDb.CreateOrGetUser(user.UserId);
                    if (user.BikeDataProjectId == null)
                    {
                        // update the local user with the user id in the contributions db.
                        user.BikeDataProjectId = contributionsDbUser.Id;
                        _db.Update(user);
                        await _db.SaveChangesAsync();
                    }
                    
                    // convert to contributions.
                    foreach (var contribution in tcxParsed.ToContributions())
                    {
                        await _contributionsDb.Contributions.AddAsync(contribution);
                        await _contributionsDb.SaveChangesAsync();

                        var fitBitContribution = new Contribution()
                        {
                            UserId = user.Id,
                            BikeDataProjectId = contribution.ContributionId,
                            FitBitLogId = activity.LogId
                        };
                        await _db.Contributions.AddAsync(fitBitContribution);
                        await _db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unhandled exception.");
            }
        }
    }
}