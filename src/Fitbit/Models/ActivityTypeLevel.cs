namespace Fitbit.Models
{
    // {
    //     "id": 1010,
    //     "maxSpeedMPH": 9.9,
    //     "minSpeedMPH": -1,
    //     "mets": 4,
    //     "name": "Very Leisurely - Less than 10 mph"
    // }      
    public class ActivityTypeLevel
    {
        public int Id { get; set; }

        public double MaxSpeedMPH { get; set; }
        
        public double MinSpeedMPH { get; set; }
        
        public int Mets { get; set; }
        
        public string Name { get; set; }
    }
}