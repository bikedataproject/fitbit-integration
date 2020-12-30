using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.FitBit.Subscriber
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
            var refreshTime = _configuration.GetValueOrDefault("refresh-time", 1000);
            
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

                await this.SetupSubscriptions(fitbitCredentials, stoppingToken);
                if (stoppingToken.IsCancellationRequested) continue;
                
                // await this.SyncDays(fitbitCredentials, stoppingToken);
                // if (stoppingToken.IsCancellationRequested) continue;
                
                await Task.Delay(refreshTime, stoppingToken);
            }
        }

        private async Task SetupSubscriptions(FitbitAppCredentials fitbitAppCredentials, CancellationToken stoppingToken)
        {
            try
            {
                var usersWithoutSubscription = await _db.Users
                    .Where(x => x.AllSynced && x.SubscriptionId == null).Take(100).ToListAsync(cancellationToken: stoppingToken);

                foreach (var user in usersWithoutSubscription)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        // create fitbit client configured for the given user.
                        var (fitbitClient, userModified) = await fitbitAppCredentials.CreateFitbitClientForUser(user);
                        if (userModified)
                        {
                            _db.Users.Update(user);
                            // ReSharper disable once MethodSupportsCancellation
                            await _db.SaveChangesAsync();
                        }
                        
                        // check if there is already a subscription.
                        var existingSubscriptions = await fitbitClient.GetSubscriptionsAsync(APICollectionType.activities);
                        var subscriptionId = string.Empty;
                        if (existingSubscriptions == null || existingSubscriptions.Count == 0)
                        {
                            // register subscription.
                            var apiSubscription = await fitbitClient.AddSubscriptionAsync(APICollectionType.activities, Guid.NewGuid().ToString());
                            subscriptionId = apiSubscription.SubscriptionId;
                        }
                        else
                        {
                            // get existing subscription id.
                            subscriptionId = existingSubscriptions.First().SubscriptionId;
                        }

                        // store the subscription.
                        user.SubscriptionId = subscriptionId;
                        _db.Users.Update(user);
                        // ReSharper disable once MethodSupportsCancellation
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to create subscription.");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unhandled exception.");
            }
        }

        private async Task SyncDays(FitbitAppCredentials fitbitAppCredentials, CancellationToken stoppingToken)
        {
            try
            {
                var dayToSync = _db.DaysToSync
                    .Where(x => !x.Synced)
                    .Include(x => x.User).FirstOrDefault();

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
                    foreach (var contribution in tcxParsed.ToContributions())
                    {
                        await _contributionsDb.SaveContribution(_db, contribution, user, activity.LogId);
                    }
                }
                
                // set as synced.
                dayToSync.Synced = true;
                _db.DaysToSync.Update(dayToSync);
                // ReSharper disable once MethodSupportsCancellation
                await _db.SaveChangesAsync();
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