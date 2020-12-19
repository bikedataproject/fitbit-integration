using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class FitbitDbContext : DbContext
    {
        public FitbitDbContext(DbContextOptions<FitbitDbContext> options) : base(options)
        {
            
        }
        
        public DbSet<AccessToken> AccessTokens { get; set; }
    }
}