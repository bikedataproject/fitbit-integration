using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;
using Fitbit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Fitbit.API.Controllers
{
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        private readonly WebhookControllerSettings _configuration;
        private readonly FitbitDbContext _db;

        public WebhookController(ILogger<WebhookController> logger, 
	        WebhookControllerSettings configuration,
	        FitbitDbContext db)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
        }

        [HttpGet]
        [Route("authorize")]
        public IActionResult Authorize()
        {
	        var callback = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/register";
	        var authenticator = new OAuth2Helper(_configuration.FitbitAppCredentials, callback);

	        var scopes = new[] {"activity","profile","location"};
	        string authUrl = authenticator.GenerateAuthUrl(scopes, null);

	        return Redirect(authUrl);
        }

	    [HttpGet]
	    [Route("register")]
        public async Task<IActionResult> Register(string code)
        {
	        _logger.LogInformation($"Request to register: {code}");

	        var callback = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/register";
	        var authenticator = new OAuth2Helper(_configuration.FitbitAppCredentials, callback);

	        var newToken = await authenticator.ExchangeAuthCodeForAccessTokenAsync(code);
	        if (newToken == null)
	        {
		        _logger.LogError("Getting access token failed!");
		        return new NotFoundResult();
	        }

	        var exitingToken = (from accessTokens in _db.AccessTokens
		        where accessTokens.UserId == newToken.UserId
		        select accessTokens).FirstOrDefault();
	        if (exitingToken != null)
	        {
		        exitingToken.Scope = newToken.Scope;
		        exitingToken.Token = newToken.Token;
		        exitingToken.ExpiresIn = newToken.ExpiresIn;
		        exitingToken.RefreshToken = newToken.RefreshToken;
		        exitingToken.TokenType = newToken.TokenType;

		        _db.AccessTokens.Update(exitingToken);
	        }
	        else
	        {
		        exitingToken = new AccessToken
		        {
			        UserId = newToken.UserId,
			        Scope = newToken.Scope,
			        Token = newToken.Token,
			        ExpiresIn = newToken.ExpiresIn,
			        RefreshToken = newToken.RefreshToken,
			        TokenType = newToken.TokenType
		        };

		        await _db.AccessTokens.AddAsync(exitingToken);
	        }

	        await _db.SaveChangesAsync();
	        
	        return new OkResult();
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
