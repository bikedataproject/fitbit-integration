using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.Fitbit.Db
{
    public class SetupTask
    {
        private readonly FitbitDbContext _db;

        public SetupTask(FitbitDbContext db)
        {
            _db = db;
        }
        
        public async Task Run()
        {
            // first apply migrations.
            await _db.Database.MigrateAsync();
        }
    }
}