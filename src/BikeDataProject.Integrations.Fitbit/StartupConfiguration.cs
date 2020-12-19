using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.Fitbit
{
    public class StartupConfiguration
    {
        public FitbitAppCredentials FitbitAppCredentials { get; set; }
        
        public string SubscriptionVerificationCode { get; set; }
    }
}