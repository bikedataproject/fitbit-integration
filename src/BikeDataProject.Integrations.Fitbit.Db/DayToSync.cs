using System;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class DayToSync
    {
        /// <summary>
        /// The id.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// The id of the user this contribution is for.
        /// </summary>
        public int UserId { get; set; }
        
        /// <summary>
        /// The user this contribution is for.
        /// </summary>
        public User User { get; set; }
        
        /// <summary>
        /// The day of the un synced activity.
        /// </summary>
        public DateTime Day { get; set; }
        
        /// <summary>
        /// The flag set if the activities got synced.
        /// </summary>
        public bool Synced { get; set; }
    }
}