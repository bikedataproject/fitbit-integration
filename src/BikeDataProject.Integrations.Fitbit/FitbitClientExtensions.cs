using System.Collections.Generic;
using System.Threading.Tasks;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using TCX.Domain;

namespace BikeDataProject.Integrations.Fitbit
{
    public static class FitbitClientExtensions
    {
        public static async Task<IEnumerable<int>> GetBicycleActivityTypes(this FitbitClient client)
        {
            // get activity types.
            var types = await client.GetActivityCategoryListAsync();
                    
            // the activity id with name 'Bicycling'.
            var activityTypes = new HashSet<int>();
            foreach (var category in types.Categories)
            {
                if (category.Name == Constants.BicycleActivityTypeName)
                {
                    foreach (var activity in category.Activities)
                    {
                        activityTypes.Add(activity.Id);
                    }
                }

                if (category.SubCategories == null) continue;
                        
                foreach (var subCategory in category.SubCategories)
                {
                    if (subCategory.Name == Constants.BicycleActivityTypeName)
                    {
                        foreach (var activity in subCategory.Activities)
                        {
                            activityTypes.Add(activity.Id);
                        }
                    }
                }
            }

            return activityTypes;
        }

        public static async Task<TrainingCenterDatabase?> GetTcxForActivity(this FitbitClient client,
            Activities activity)
        {
            if (string.IsNullOrWhiteSpace(activity.TcxLink)) return null;
            
            var tcx = await client.GetApiFreeResponseAsync(activity.TcxLink);
            if (string.IsNullOrWhiteSpace(tcx)) return null;
            
            return TCX.Parser.Parse(tcx);
        }
    }
}