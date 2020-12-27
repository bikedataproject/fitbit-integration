using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class FitbitDbContext : DbContext
    {
        public FitbitDbContext(DbContextOptions<FitbitDbContext> options) : base(options)
        {
            
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Contribution> Contributions { get; set; }
        public DbSet<UserUpdatedResource> UserUpdatedResources { get; set; }
    }
}