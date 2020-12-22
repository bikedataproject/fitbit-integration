namespace BikeDataProject.Integrations.Fitbit.Db
{
    /// <summary>
    /// Represents a contribution from fitbit with an id set if the contribution is in the contributions db.
    /// </summary>
    public class Contribution
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
        /// The id of the contribution in the bike data project db.
        /// </summary>
        public int BikeDataProjectId { get; set; }
        
        /// <summary>
        /// The log id in the fitbit api.
        /// </summary>
        public long FitBitLogId { get; set; }
    }
}