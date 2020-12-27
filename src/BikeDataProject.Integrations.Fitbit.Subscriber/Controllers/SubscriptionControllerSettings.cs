using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.FitBit.Subscriber.Controllers
{
    public class SubscriptionControllerSettings
    {
        public FitbitAppCredentials FitbitAppCredentials { get; set; }
        
        public string SubscriptionVerificationCode { get; set; }
    }
}