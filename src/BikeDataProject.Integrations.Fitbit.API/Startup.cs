using System.IO;
using BikeDataProject.Integrations.Fitbit.API.Controllers;
using BikeDataProject.Integrations.Fitbit.Db;
using Fitbit.Api.Portable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BikeDataProject.Integrations.Fitbit.API
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // setup logging.
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration)
                .CreateLogger();
            services.AddLogging(b => { b.AddSerilog(); });
            
            // read/parse fitbit configurations.
            var fitbitCredentials = new FitbitAppCredentials()
            {
                ClientId = _configuration["FITBIT_CLIENT_ID"],
                ClientSecret = File.ReadAllText(_configuration["FITBIT_CLIENT_SECRET"])
            };
            services.AddSingleton(new WebhookControllerSettings()
            {
                FitbitAppCredentials = fitbitCredentials,
                LandingPage = _configuration["FITBIT_LANDING"]
            });
            
            // configure fitbit db access.
            services.AddDbContext<FitbitDbContext>(options => options.UseNpgsql(
                File.ReadAllText(_configuration["FITBIT_DB"])));
            
            // add controllers.
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedNGINXHeaders();
            
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
