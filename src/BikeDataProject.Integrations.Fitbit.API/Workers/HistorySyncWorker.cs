using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.API.Workers
{
    public class HistorySyncWorker : BackgroundService
    {
        private readonly ILogger<HistorySyncWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly FitbitDbContext _db;
        private readonly DB.BikeDataDbContext _contributionsDb;
        private readonly HashSet<int> _activityTypes = new ();

        public HistorySyncWorker(ILogger<HistorySyncWorker> logger, IConfiguration configuration,
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
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(HistorySyncWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>("refresh-time", 1000));

                var enabled = _configuration.GetValueOrDefault("SYNC_HISTORY", true);
                if (!enabled)
                {
                    _logger.LogWarning($"{nameof(HistorySyncWorker)} is not enabled.");
                    
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                var doSync = FitbitApiState.IsReady();
                if (!doSync)
                {
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                await this.RunAsync(stoppingToken);
                
                await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {
                // read/parse fitbit configurations.
                var fitbitAppCredentials = new FitbitAppCredentials()
                {
                    ClientId = _configuration["FITBIT_CLIENT_ID"],
                    ClientSecret = await File.ReadAllTextAsync(_configuration["FITBIT_CLIENT_SECRET"], stoppingToken)
                };
                
                var user = (from users in _db.Users
                    where users.AllSynced == false
                    select users).FirstOrDefault();

                // no user found without history un synced.
                if (user == null) return;

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
                var after = user.LatestSyncedStamp ?? (new DateTime(1970, 1, 1)).ToUniversalTime();
                var activities = await fitbitClient.GetActivityLogsListAsync(null, after);
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
                    
                    // create user in contributions db if needed and keep the id.
                    contributionsDbUser ??= await _contributionsDb.CreateOrGetUser(_db, user);
                    
                    // make sure not to hit rate limit.
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);

                    // get tcx.
                    var tcxParsed = await fitbitClient.GetTcxForActivity(activity);

                    // convert to contributions.
                    var parseContributions = tcxParsed?.ToContributions(_logger);
                    if (parseContributions == null) continue;
                    foreach (var contribution in parseContributions)
                    {
                        await _contributionsDb.SaveContribution(_db, contribution, user, activity.LogId);
                    }

                    _logger.LogInformation("Activity {logId} for {userId} synchronized.",
                        activity.LogId, user.UserId);
                }

                // set history as saved.
                user.AllSynced = true;
                _db.Users.Update(user);
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