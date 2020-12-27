using System;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;

namespace BikeDataProject.Integrations.Fitbit
{
    public static class FitbitAppCredentialsExtensions
    {
        public static async Task<(FitbitClient client, bool userModified)> CreateFitbitClientForUser(this FitbitAppCredentials fitbitAppCredentials, User user)
        {
            // try to get historic activities.
            var accessToken = new OAuth2AccessToken()
            {
                Scope = user.Scope,
                ExpiresIn = user.ExpiresIn,
                RefreshToken = user.RefreshToken,
                Token = user.Token,
                TokenType = user.TokenType,
                UserId = user.UserId
            };
            var fitbitClient = new FitbitClient(fitbitAppCredentials, accessToken);
                
            // refresh token if needed.
            var userModified = false;
            if (!accessToken.IsFresh(user.TokenCreated))
            {
                // refresh token.
                accessToken = await fitbitClient.RefreshOAuth2TokenAsync();

                // update details.
                user.Scope = accessToken.Scope;
                user.Token = accessToken.Token;
                user.ExpiresIn = accessToken.ExpiresIn;
                user.RefreshToken = accessToken.RefreshToken;
                user.TokenType = accessToken.TokenType;
                user.TokenCreated = DateTime.UtcNow;
                userModified = true;
            }

            return (fitbitClient, userModified);
        }
    }
}