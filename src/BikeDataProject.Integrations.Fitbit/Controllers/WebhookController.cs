﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;
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
        [Route("authorize")]
        public IActionResult Authorize()
        {
	        var callback = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/register/";
	        var authenticator = new OAuth2Helper(_configuration.FitbitAppCredentials, callback);

	        var scopes = new[] {"activity"};
	        string authUrl = authenticator.GenerateAuthUrl(scopes, null);

	        return Redirect(authUrl);
        }

	    [HttpGet]
	    [Route("register")]
        public async Task<IActionResult> Register(string code)
        {
	        _logger.LogInformation($"Request to register: {code}");

	        var callback = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/register/";
	        var authenticator = new OAuth2Helper(_configuration.FitbitAppCredentials, callback);

	        var accessToken = await authenticator.ExchangeAuthCodeForAccessTokenAsync(code);
	        
	        // var client = new FitbitClient()
	        return new NotFoundResult();
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
        public IActionResult SubscriptionData()
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
		        _logger.LogInformation($"Received: {updatedResource.CollectionType}");
		        //do something here with the updated resources fitbit is reporting

		        //likely, you'll grab stored AuthToken/AuthSecret 
		        // for a user and use FitbitClient class to get the latest resource type
	        }

	        return new NoContentResult();
        }
    }
}
