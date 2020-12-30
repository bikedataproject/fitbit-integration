using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.API;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.FitBit.API.Controllers
{
	[ApiController]
	public class SubscriptionController : ControllerBase
	{
		private readonly ILogger<SubscriptionController> _logger;
		private readonly SubscriptionControllerSettings _configuration;
		private readonly FitbitDbContext _db;
		private readonly HashSet<int> _activityTypes = new();

		public SubscriptionController(ILogger<SubscriptionController> logger,
			SubscriptionControllerSettings configuration,
			FitbitDbContext db)
		{
			_logger = logger;
			_configuration = configuration;
			_db = db;
		}

		private DateTime _lastActivityTypeSync = DateTime.Now;

		[HttpGet]
		[Route("subscriptions")]
		public IActionResult Verify([FromQuery] string? verify)
		{
			try
			{
				_logger.LogInformation($"Request to verify: {verify}");

				// implements verification mechanism as described:
				// https://dev.fitbit.com/build/reference/web-api/subscriptions/#verify-a-subscriber
				if (string.IsNullOrWhiteSpace(verify)) return new NotFoundResult();

				// this is a verification request, response if the code matches.
				if (_configuration.SubscriptionVerificationCode == verify)
				{
					return new NoContentResult();
				}
				return new NotFoundResult();
			}
			catch (Exception e)
			{
				_logger.LogError(e, $"{nameof(SubscriptionController)}.{nameof(Verify)}");
				throw;
			}
		}

		[HttpPost]
		[Route("subscriptions")]
		public async Task<IActionResult> SubscriptionData()
		{
			try
			{
				var subscriptionManager = new SubscriptionManager();
				List<UpdatedResource> updatedResources; //all the updated users and which resources

				using (var sr = new StreamReader(Request.Body))
				{
					var responseText = await sr.ReadToEndAsync();
					//note, you can store the raw response from fitbit here if you like
					
					_logger.LogDebug($"Fitbit subscription post: {responseText}");

					responseText = subscriptionManager.StripSignatureString(responseText);
					updatedResources = subscriptionManager.ProcessUpdateReponseBody(responseText);
				}

				foreach (var updatedResource in updatedResources)
				{
					_logger.LogInformation($"Received: {updatedResource.CollectionType}");

					if (updatedResource.CollectionType != APICollectionType.activities) continue;

					// get the user associated with the subscription id.
					var user = _db.GetUserForSubscription(updatedResource.SubscriptionId);
					if (user == null)
					{
						_logger.LogError(
							$"No user was found for subscription id: {updatedResource.SubscriptionId}");
						continue;
					}

					// save update resource.
					var ur = _db.DaysToSync
						.FirstOrDefault(x => x.UserId == user.Id && x.Day == updatedResource.Date);
					if (ur != null)
					{
						ur.Synced = false;
						_db.DaysToSync.Update(ur);
					}

					if (ur == null)
					{
						ur = new DayToSync()
						{
							UserId = user.Id,
							User = user,
							Day = updatedResource.Date,
							Synced = false
						};
						await _db.DaysToSync.AddAsync(ur);
					}

					await _db.SaveChangesAsync();
				}

				return new NoContentResult();
			}
			catch (Exception e)
			{
				_logger.LogError(e, $"{nameof(SubscriptionController)}.{nameof(SubscriptionData)}");
				throw;
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