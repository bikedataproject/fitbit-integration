using System;
using System.Collections.Generic;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    /// <summary>
    /// Represents a fitbit user that has synced their account to the bike data project.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The id.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// The access token.
        /// </summary>
        public string Token { get; set; }
        
        /// <summary>
        /// The type of the access token.
        /// </summary>
        public string TokenType { get; set; }
        
        /// <summary>
        /// The scope of the access token.
        /// </summary>
        public string Scope { get; set; }
        
        /// <summary>
        /// The lifetime of the token in seconds.
        /// </summary>
        public int ExpiresIn { get; set; }
        
        /// <summary>
        /// The refresh token, for use when the token is expired.
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// The fitbit user id.
        /// </summary>
        public string UserId { get; set; }
        
        /// <summary>
        /// The timestamp for when the token was created.
        /// </summary>
        public DateTime TokenCreated { get; set; }
        
        /// <summary>
        /// All the history was synced.
        /// </summary>
        public bool AllSynced { get; set; }
        
        /// <summary>
        /// The timestamp of the latest synced activity.
        /// </summary>
        public DateTime? LatestSyncedStamp { get; set; }

        /// <summary>
        /// The id of this user in the bike data project db.
        /// </summary>
        public int? BikeDataProjectId { get; set; }

        /// <summary>
        /// The contributions associated with this user.
        /// </summary>
        public List<Contribution> Contributions { get; set; }
    }
}