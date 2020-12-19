namespace Fitbit.Models
{
    public class ActivityCategory
    {
        public ActivityType[] Activities { get; set; }

        public int Id { get; set; }

        public string Name { get; set; }
        
        public ActivityCategory[] SubCategories { get; set; }
    }
}