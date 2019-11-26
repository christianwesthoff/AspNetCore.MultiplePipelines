using mass_transit.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace mass_transit
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
//            services.AddServiceProviderBridge();
            services.AddEventBus(typeof(Startup).Assembly);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseBranchWithServices("Api", "/api", 
                services => 
                {
                    services.AddControllers();
                    services.AddScoped<TestConsumer>();
                }, 
                api =>
                {
                    
                    if (env.IsDevelopment())
                    {
                        api.UseDeveloperExceptionPage();
                    }

                    api.UseHttpsRedirection();

                    api.UseRouting();

                    api.UseAuthorization();

                    api.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                }, typeof(Startup).Assembly, typeof(IBusControl), typeof(ISendEndpoint), typeof(IPublishEndpoint));
            app.UseBranchWithServices("Mvc", "",
                services =>
                {
                    services.AddControllers();
                }, api =>
                {
                    if (env.IsDevelopment())
                    {
                        api.UseDeveloperExceptionPage();
                    }

                    api.UseHttpsRedirection();

                    api.UseRouting();

                    api.UseAuthorization();

                    api.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                }, typeof(Startup).Assembly, typeof(IBusControl), typeof(ISendEndpoint), typeof(IPublishEndpoint));
        }
    }
}