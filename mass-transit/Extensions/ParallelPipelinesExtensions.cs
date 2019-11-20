using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;

namespace mass_transit.Extensions
{
    public interface IParallelPipelineServiceProviderBridge
    {
        ICollection<IServiceProvider> PipelineServiceProviders { get; }
    }

    public class ParallelPipelineServiceProviderBridge : IParallelPipelineServiceProviderBridge
    {
        public ParallelPipelineServiceProviderBridge()
        {
            PipelineServiceProviders = new List<IServiceProvider>();
        }
        
        public ICollection<IServiceProvider> PipelineServiceProviders { get; }
    }

    public static class ParallelServiceProviderBridgeServiceExtension
    {
        public static IServiceCollection AddServiceProviderBridge(this IServiceCollection services)
        {
            services.AddSingleton<IParallelPipelineServiceProviderBridge, ParallelPipelineServiceProviderBridge>();
            return services;
        }
    }
    
    public static class ParallelPipelinesExtensions
    {

        /// <summary>
        /// Sets up an application branch with an isolated DI container
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="pipelineName">Pipeline name</param>
        /// <param name="path">Relative path for the application branch</param>
        /// <param name="servicesConfiguration">DI container configuration</param>
        /// <param name="appBuilderConfiguration">Application pipeline configuration for the created branch</param>
        /// <param name="sharedTypes">Shared types</param>
        public static IApplicationBuilder UseBranchWithServices(this IApplicationBuilder app, string pipelineName, PathString path, 
            Action<IServiceCollection> servicesConfiguration, Action<IApplicationBuilder> appBuilderConfiguration, params Type[] sharedTypes)
        {
            return app.UseBranchWithServices(pipelineName, new[] { path }, servicesConfiguration, appBuilderConfiguration, sharedTypes);
        }

        /// <summary>
        /// Sets up an application branch with an isolated DI container with several routes (entry points)
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="pipelineName">Pipeline name</param>
        /// <param name="paths">Relative paths for the application branch</param>
        /// <param name="servicesConfiguration">DI container configuration</param>
        /// <param name="appBuilderConfiguration">Application pipeline configuration for the created branch</param>
        /// <param name="sharedTypes">Shared types</param>
        public static IApplicationBuilder UseBranchWithServices(this IApplicationBuilder app, string pipelineName, IEnumerable<PathString> paths,
            Action<IServiceCollection> servicesConfiguration, Action<IApplicationBuilder> appBuilderConfiguration, params Type[] sharedTypes)
        {
            var webHost = new WebHostBuilder().
                ConfigureServices(s => {
                    s.AddSingleton<IServer, DummyServer>();
                    s.AddSingleton<IPipelineIdentity>(_ => new PipelineIdentity(pipelineName, paths));
                    sharedTypes.ForEach(type => s.AddTransient(type,
                        sp => app.ApplicationServices
                                  .GetService(type) ?? 
                              throw new NotSupportedException($"Shared type \"{type}\" is not registered in core service provider.")));
                }).
                ConfigureServices(servicesConfiguration).
                UseStartup<EmptyStartup>().
                Build();
            
            var serviceProvider = webHost.Services;
            var serverFeatures = webHost.ServerFeatures;

            var bridge = app.ApplicationServices.GetRequiredService<IParallelPipelineServiceProviderBridge>();            
            bridge.PipelineServiceProviders.Add(serviceProvider);
            
            var appBuilderFactory = serviceProvider.GetRequiredService<IApplicationBuilderFactory>();
            var branchBuilder = appBuilderFactory.CreateBuilder(serverFeatures);
            var factory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            branchBuilder.Use(async (context, next) =>
            {
                var coreServiceProvider = context.RequestServices;
                using (var scope = factory.CreateScope())
                {
                    context.RequestServices = scope.ServiceProvider;

                    var coreServiceProviderAccessor = context.RequestServices
                        .GetService<ICoreServiceProviderAccessor>();
                    if (coreServiceProviderAccessor != null)
                        coreServiceProviderAccessor.ServiceProvider = coreServiceProvider;
                    
                    var httpContextAccessor = context.RequestServices
                        .GetService<IHttpContextAccessor>();
                    if (httpContextAccessor != null)
                        httpContextAccessor.HttpContext = context;

                    await next();
                }
                context.RequestServices = coreServiceProvider;
            });

            appBuilderConfiguration(branchBuilder);

            var branchDelegate = branchBuilder.Build();

            foreach (var path in paths)
            {
                app.Map(path, builder =>
                {
                    builder.Use(async (context, next) =>
                    {
                        await branchDelegate(context);
                    });
                });
            }

            return app;
        }

        private class PipelineIdentity : IPipelineIdentity
        {
            public PipelineIdentity(string name, IEnumerable<PathString> paths)
            {
                Name = name;
                Paths = paths;
            }
            
            public string Name { get; }
            public IEnumerable<PathString> Paths { get; }
        }

        private class EmptyStartup
        {
            public void ConfigureServices(IServiceCollection services) {}

            public void Configure(IApplicationBuilder app) {}
        }

        private class DummyServer : IServer
        {
            public IFeatureCollection Features { get; } = new FeatureCollection();

            public void Dispose() {}

            public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }

    public interface IPipelineIdentity
    {
        string Name { get; }
        IEnumerable<PathString> Paths { get; }
    }
    
    public interface ICoreServiceProviderAccessor
    {
        IServiceProvider ServiceProvider { get; set; }
    }
}