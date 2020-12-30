using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.FitBit.API.Controllers
{
    public class SubscriptionControllerSettings
    {
        public FitbitAppCredentials FitbitAppCredentials { get; init; }
        
        public string SubscriptionVerificationCode { get; init; }
    }
}