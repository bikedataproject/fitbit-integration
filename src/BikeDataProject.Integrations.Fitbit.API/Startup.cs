using System.IO;
using BikeDataProject.Integrations.Fitbit.API.Controllers;
using BikeDataProject.Integrations.FitBit.API.Controllers;
using Fitbit.Api.Portable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
            var subVerCode = File.ReadAllText(_configuration["FITBIT_SUB_VER_CODE"]);
            services.AddSingleton(new SubscriptionControllerSettings()
            {
                FitbitAppCredentials = fitbitCredentials,
                SubscriptionVerificationCode = subVerCode
            });
            
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
