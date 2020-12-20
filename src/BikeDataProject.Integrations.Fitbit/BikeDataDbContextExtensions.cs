using System;
using System.Linq;
using System.Threading.Tasks;
using BikeDataProject.DB.Domain;

namespace BikeDataProject.Integrations.Fitbit
{
    public static class BikeDataDbContextExtensions
    {
        public static async Task<User> CreateOrGetUser(this BikeDataDbContext dbContext, string userId,
            string provider = Constants.FitbitProviderName)
        {
            var user = (from u in dbContext.Users
                where u.ProviderUser == userId &&
                      u.Provider == provider
                select u).FirstOrDefault();
            if (user == null)
            {
                user = new User()
                {
                    Provider = Constants.FitbitProviderName,
                    ProviderUser = userId
                };
                await dbContext.Users.AddAsync(user);
                await dbContext.SaveChangesAsync();
            }

            return user;
        }
    }
}