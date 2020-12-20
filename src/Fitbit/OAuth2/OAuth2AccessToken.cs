using System;
using Newtonsoft.Json;

namespace Fitbit.Api.Portable.OAuth2
{
    public class OAuth2AccessToken
    {
        [JsonProperty("access_token")]
        public string Token { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; } // "Bearer" is expected

        [JsonProperty("scope")]
        public string Scope { get; set; }
        
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; } //maybe convert this to a DateTime ?

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        public bool IsFresh(DateTime tokenCreationDateTime, int tolerance = 600)
        {
            var expirationDate = tokenCreationDateTime.AddSeconds(ExpiresIn).AddSeconds(-tolerance);
            if (expirationDate < DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }
    }
}
