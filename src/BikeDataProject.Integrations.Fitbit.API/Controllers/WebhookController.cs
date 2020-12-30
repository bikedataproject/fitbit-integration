using System;
using System.Linq;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable.OAuth2;
using Microsoft.AspNetCore.Mvc;
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
	        
	        _logger.LogDebug($"Authorization requested.");

	        return Redirect(authUrl);
        }

	    [HttpGet]
	    [Route("register")]
        public async Task<IActionResult> Register(string code)
        {
	        try
	        {
		        _logger.LogDebug($"Request to register: {code}");

		        var callback = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/register";
		        var authenticator = new OAuth2Helper(_configuration.FitbitAppCredentials, callback);
	        
		        _logger.LogInformation($"Authenticator created!");
		        _logger.LogWarning($"Authenticator created!");

		        var newToken = await authenticator.ExchangeAuthCodeForAccessTokenAsync(code);
	        
		        _logger.LogInformation($"Token exchanged!");
		        _logger.LogWarning($"Token exchanged!");
	        
		        if (newToken == null)
		        {
			        _logger.LogError("Getting access token failed!");
			        return new NotFoundResult();
		        }

		        var exitingToken = (from accessTokens in _db.Users
			        where accessTokens.UserId == newToken.UserId
			        select accessTokens).FirstOrDefault();
		        if (exitingToken != null)
		        {
			        exitingToken.Scope = newToken.Scope;
			        exitingToken.Token = newToken.Token;
			        exitingToken.ExpiresIn = newToken.ExpiresIn;
			        exitingToken.RefreshToken = newToken.RefreshToken;
			        exitingToken.TokenType = newToken.TokenType;
			        exitingToken.TokenCreated = DateTime.UtcNow;

			        _db.Users.Update(exitingToken);
		        }
		        else
		        {
			        exitingToken = new User
			        {
				        UserId = newToken.UserId,
				        Scope = newToken.Scope,
				        Token = newToken.Token,
				        ExpiresIn = newToken.ExpiresIn,
				        RefreshToken = newToken.RefreshToken,
				        TokenType = newToken.TokenType,
				        TokenCreated = DateTime.UtcNow
			        };

			        await _db.Users.AddAsync(exitingToken);
		        }

		        await _db.SaveChangesAsync();
	        
		        return Redirect(_configuration.LandingPage);
	        }
	        catch (Exception e)
	        {
		        _logger.LogError(e, "Unhandled exception.");
		        throw;
	        }
        }
    }
}
