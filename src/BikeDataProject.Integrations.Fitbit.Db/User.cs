using System;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class User
    {
        public int Id { get; set; }
        
        public string Token { get; set; }
        
        public string TokenType { get; set; }
        
        public string Scope { get; set; }
        
        public int ExpiresIn { get; set; }
        
        public string RefreshToken { get; set; }

        public string UserId { get; set; }
        
        public DateTime TokenCreated { get; set; }
        
        public bool AllSynced { get; set; }
        
        public DateTime? LatestSyncedStamp { get; set; }
    }
}