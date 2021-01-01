using System;
using Fitbit.Api.Portable;

namespace BikeDataProject.Integrations.Fitbit.API.Workers
{
    internal static class FitbitApiState
    {
        public static DateTime? RetryAfter = null;

        public static bool IsReady()
        {
            if (RetryAfter != null &&
                RetryAfter.Value >= DateTime.Now.ToUniversalTime())
            {
                return false;
            }

            return true;
        }

        public static void HandleRateLimitException(FitbitRateLimitException e)
        {
            RetryAfter = e.RetryAfter;
        }
    }
}