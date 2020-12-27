using System.Linq;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public static class FitbitDbContextExtensions
    {
        public static bool UserHasContributionWithLogId(this FitbitDbContext db, User user, long logId)
        {
            var contribution = (from c in db.Contributions
                where c.FitBitLogId == logId && c.UserId == user.Id
                select c).FirstOrDefault();
            return contribution != null;
        }

        public static User GetUserForSubscription(this FitbitDbContext db, string subscriptionId)
        {
            var user = (from u in db.Users
                where u.SubscriptionId == subscriptionId
                select u).FirstOrDefault();
            return user;
        }
    }
}