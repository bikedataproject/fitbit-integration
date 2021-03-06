using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.API.Workers
{
    public class SubscriptionManagerWorker : BackgroundService
    {
        private readonly ILogger<SubscriptionManagerWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly FitbitDbContext _db;
        private readonly DB.BikeDataDbContext _contributionsDb;

        public SubscriptionManagerWorker(ILogger<SubscriptionManagerWorker> logger, IConfiguration configuration,
            FitbitDbContext db, DB.BikeDataDbContext contributionsDb)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
            _contributionsDb = contributionsDb;
        }
        
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
                    nameof(SubscriptionManagerWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>("refresh-time", 1000));

                var enabled = _configuration.GetValueOrDefault("SETUP_SUBSCRIPTIONS", true);
                if (!enabled)
                {
                    _logger.LogWarning($"{nameof(SubscriptionManagerWorker)} is not enabled.");
                    
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                var doSync = FitbitApiState.IsReady();
                if (!doSync)
                {
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                await this.SetupSubscriptions(fitbitCredentials, stoppingToken);
                if (stoppingToken.IsCancellationRequested) continue;
                
                await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
            }
        }

        private async Task SetupSubscriptions(FitbitAppCredentials fitbitAppCredentials, CancellationToken stoppingToken)
        {
            try
            {
                var usersWithoutSubscription = await _db.Users
                    .Where(x => x.AllSynced && x.SubscriptionId == null)
                    .OrderBy(x => x.Id)
                    .Take(1).ToListAsync(cancellationToken: stoppingToken);

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
    }
}