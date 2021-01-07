using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.API.Workers
{
    public class SubscriptionSyncWorker : BackgroundService
    {
        private readonly ILogger<SubscriptionSyncWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly FitbitDbContext _db;
        private readonly DB.BikeDataDbContext _contributionsDb;
        private readonly HashSet<int> _activityTypes = new ();

        public SubscriptionSyncWorker(ILogger<SubscriptionSyncWorker> logger, IConfiguration configuration,
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
            // read/parse fitbit configurations.
            var fitbitCredentials = new FitbitAppCredentials()
            {
                ClientId = _configuration["FITBIT_CLIENT_ID"],
                ClientSecret = await File.ReadAllTextAsync(_configuration["FITBIT_CLIENT_SECRET"], stoppingToken)
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(SubscriptionSyncWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault("refresh-time", 1000));

                var enabled = _configuration.GetValueOrDefault("SYNC_SUBSCRIPTIONS", true);
                if (!enabled)
                {
                    _logger.LogWarning($"{nameof(SubscriptionSyncWorker)} is not enabled.");
                    
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                var doSync = FitbitApiState.IsReady();
                if (!doSync)
                {
                    await Task.Delay(_configuration.GetValueOrDefault("refresh-time", 1000), stoppingToken);
                    continue;
                }
                
                await this.SyncDays(fitbitCredentials, stoppingToken);
                if (stoppingToken.IsCancellationRequested) continue;
                
                await Task.Delay(_configuration.GetValueOrDefault("refresh-time", 1000), stoppingToken);
            }
        }

        private async Task SyncDays(FitbitAppCredentials fitbitAppCredentials, CancellationToken stoppingToken)
        {
            try
            {
                // optionally only sync after the day has passed, this means we sync each day for each users once at maximum.
                var syncAfterDay = _configuration.GetValueOrDefault<bool>("SYNC_SUBSCRIPTIONS_AFTER_DAY", false);
                DayToSync? dayToSync;
                if (!syncAfterDay)
                {
                    dayToSync = _db.DaysToSync
                        .Where(x => !x.Synced)
                        .Include(x => x.User).FirstOrDefault();
                }
                else
                {
                    // go 26hrs back and that date is the last date to sync.
                    var lastDate = DateTime.Now.ToUniversalTime().AddHours(-26);
                    lastDate = new DateTime(lastDate.Year, lastDate.Month, lastDate.Day, 0, 0, 0,
                        DateTimeKind.Unspecified);
                    dayToSync = _db.DaysToSync
                        .Where(x => !x.Synced && x.Day <= lastDate)
                        .Include(x => x.User).FirstOrDefault();
                }

                // no un synced updated resources.
                if (dayToSync == null) return;
                var user = dayToSync.User;
                
                // create fitbit client configured for the given user.
                var (fitbitClient, userModified) = await fitbitAppCredentials.CreateFitbitClientForUser(user);
                if (userModified)
                {
                    _db.Users.Update(user);
                    // ReSharper disable once MethodSupportsCancellation
                    await _db.SaveChangesAsync();
                }

                // get activity types if needed.
                // make sure to refresh once in a while.
                if (!await this.SyncActivityTypes(fitbitClient)) return;

                // get cycling activities.
                var activities = await fitbitClient.GetActivityLogsListAsync(dayToSync.Day);
                if (activities?.Activities == null) return;

                // sync all activities.
                DB.User? contributionsDbUser = null;
                foreach (var activity in activities.Activities)
                {
                    // if not a cycling activity, ignore.
                    if (!_activityTypes.Contains(activity.ActivityTypeId)) continue;
                    
                    if (stoppingToken.IsCancellationRequested) break;
                    
                    // check if activity with log id was already included.
                    if (_db.UserHasContributionWithLogId(user, activity.LogId)) continue;
                    
                    // get tcx.
                    var tcxParsed = await fitbitClient.GetTcxForActivity(activity);
                    if (tcxParsed == null) continue;
                    
                    // create user in contributions db if needed and keep the id.
                    contributionsDbUser ??= await _contributionsDb.CreateOrGetUser(_db, user);
                    
                    // convert to contributions.
                    var parsedContributions = tcxParsed.ToContributions(_logger);
                    if (parsedContributions == null) continue;
                    foreach (var contribution in parsedContributions)
                    {
                        await _contributionsDb.SaveContribution(_db, contribution, user, activity.LogId);
                    }
                   
                    _logger.LogInformation("Activity {logId} for {userId} synchronized.", 
                        activity.LogId, user.UserId);
                }
                
                // set as synced.
                dayToSync.Synced = true;
                _db.DaysToSync.Update(dayToSync);
                // ReSharper disable once MethodSupportsCancellation
                await _db.SaveChangesAsync();
            }
            catch (FitbitRateLimitException e)
            {
                _logger.LogCritical(e, "Rate limit hit, retrying at {seconds}.", e.RetryAfter);
                FitbitApiState.HandleRateLimitException(e);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unhandled exception.");
            }
        }

        private async Task<bool> SyncActivityTypes(FitbitClient fitbitClient)
        {
            if ((DateTime.Now - _lastActivityTypeSync).TotalHours > 2)
            {
                _activityTypes.Clear();
            }
            
            if (_activityTypes.Count == 0)
            {
                _activityTypes.UnionWith(await fitbitClient.GetBicycleActivityTypes());
                    
                _lastActivityTypeSync = DateTime.Now;
            }
            
            if (_activityTypes.Count == 0)
            {
                _logger.LogCritical("Bicycling activity types not found, cannot synchronize activities without them.");
                return false;
            }

            return true;
        }
    }
}