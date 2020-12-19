using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.Fitbit.Controllers
{
    public class WebHookControllerSettings
    {
        public FitbitAppCredentials FitbitAppCredentials { get; set; }
        
        public string SubscriptionVerificationCode { get; set; }
    }
}