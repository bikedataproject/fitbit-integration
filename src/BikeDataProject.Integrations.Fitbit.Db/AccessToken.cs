using System;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class AccessToken
    {
        public int Id { get; set; }
        
        public string Token { get; set; }
        
        public string TokenType { get; set; }
        
        public string Scope { get; set; }
        
        public int ExpiresIn { get; set; }
        
        public string RefreshToken { get; set; }

        public string UserId { get; set; }
    }
}