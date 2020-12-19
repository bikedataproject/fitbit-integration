namespace Fitbit.Models
{
    public class ActivityType
    {
        public string AccessLevel { get; set; }
        
        public ActivityTypeLevel[] ActivityLevels { get; set; }
        
        public bool HasSpeed { get; set; }
        
        public int Id { get; set; }
        
        public string Name { get; set; }
    }
}