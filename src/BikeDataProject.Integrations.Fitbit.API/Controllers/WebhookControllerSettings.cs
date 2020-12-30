using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.Fitbit.API.Controllers
{
    public class WebhookControllerSettings
    {
        public FitbitAppCredentials FitbitAppCredentials { get; init; }
        
        public string LandingPage { get; init; }
    }
}