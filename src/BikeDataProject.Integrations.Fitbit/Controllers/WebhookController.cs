using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fitbit.Api.Portable;
using Fitbit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.Controllers
{
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        private readonly WebHookControllerSettings _configuration;

        public WebhookController(ILogger<WebhookController> logger, 
	        WebHookControllerSettings configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/")]
        public IActionResult Verify([FromQuery] string? verify)
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

        [HttpPost]
        [Route("/")]
        public IActionResult SubscriptionData( string? verify)
        {
	        var subscriptionManager = new SubscriptionManager();
	        List<UpdatedResource> updatedResources; //all the updated users and which resources

	        using (var sr = new StreamReader(Request.Body))
	        {
		        var responseText = sr.ReadToEnd();
		        //note, you can store the raw response from fitbit here if you like

		        responseText = subscriptionManager.StripSignatureString(responseText);
		        updatedResources = subscriptionManager.ProcessUpdateReponseBody(responseText);
	        }

	        foreach (var updatedResource in updatedResources)
	        {
		        //do something here with the updated resources fitbit is reporting

		        //likely, you'll grab stored AuthToken/AuthSecret 
		        // for a user and use FitbitClient class to get the latest resource type
	        }

	        return new NoContentResult();
        }
    }
}
