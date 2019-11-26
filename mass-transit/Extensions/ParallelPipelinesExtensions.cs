using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
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
    public interface IPipelineServiceProviderBridge
    {
        IDictionary<Assembly, IServiceProvider> PipelineServiceProviders { get; }
    }

    public class PipelineServiceProviderBridge : IPipelineServiceProviderBridge
    {
        public PipelineServiceProviderBridge()
        {
            PipelineServiceProviders = new Dictionary<Assembly, IServiceProvider>();
        }
        
        public IDictionary<Assembly, IServiceProvider> PipelineServiceProviders { get; }
    }

    public static class ParallelServiceProviderBridgeServiceExtension
    {
        public static IServiceCollection AddServiceProviderBridge(this IServiceCollection services)
        {
            services.AddSingleton<IPipelineServiceProviderBridge, PipelineServiceProviderBridge>();
            return services;
        }
    }

    public static class MultiplePipelinesWebHostConfigurationExtension
    {
        public static IWebHostBuilder ConfigureMultiplePipelines(this IWebHostBuilder webHost, Action<IServiceCollection> serviceConfiguration, Action<IMultiplePipelineBuilder> builderConfiguration)
        {
            var pipelineBuilder = new DefaultMultiplePipelineBuilder();
            builderConfiguration.Invoke(pipelineBuilder);
            var sharedServiceCollection = new ServiceCollection();
            webHost.ConfigureServices(services =>
            {
                services.AddServiceProviderBridge();
                serviceConfiguration(sharedServiceCollection);
                sharedServiceCollection.AddEventBus(configure =>
                {
                    pipelineBuilder.Branches.ForEach(branch =>
                    {
                        var pipelineAssembly = branch.StartupType.Assembly;
                        configure.ConsumerConfigurations.Add(pipelineAssembly,
                            (provider, type) =>
                            {
                                var pipelineServiceProvider = provider.GetRequiredService<IPipelineServiceProviderBridge>()
                                    .PipelineServiceProviders[pipelineAssembly];
                                using var pipelineScope = pipelineServiceProvider
                                    .GetRequiredService<IServiceScopeFactory>().CreateScope();
                                return (IConsumer)pipelineScope.ServiceProvider.GetService(type);
                            });
                    });
                });
                sharedServiceCollection.ForEach(services.Add);
            });
            webHost.Configure((env, app) =>
            {
                pipelineBuilder.Branches.ForEach(branch =>
                {
                    var startup = StartupLoader.LoadMethods(app.ApplicationServices, branch.StartupType,
                        env.HostingEnvironment.EnvironmentName);

                    app.UseBranch(branch.Name, branch.Path, (services) =>
                        {
                            sharedServiceCollection.ForEach(service => 
                                services.Add(new ServiceDescriptor(service.ServiceType, _ => app.ApplicationServices.GetService(service.ServiceType), service.Lifetime)));
                            branch.StartupType.Assembly.FindDerivedTypes(typeof(IConsumer)).ForEach(consumerType => services.AddScoped(consumerType));
                            startup.ConfigureServicesDelegate(services);
                        },
                        startup.ConfigureDelegate);
                });
            });

            return webHost;
        }
    }

    public interface IMultiplePipelineBuilder
    {
        IMultiplePipelineBuilder AddBranch<T>(string name, PathString path);
        IMultiplePipelineBuilder AddBranch(string name, PathString path, Type startupType);
    }

    public class DefaultMultiplePipelineBuilder : IMultiplePipelineBuilder
    {
        public ICollection<Branch> Branches => new List<Branch>();

        public IMultiplePipelineBuilder AddBranch<T>(string name, PathString path)
        {
            return AddBranch(name, path, typeof(T));
        }

        public IMultiplePipelineBuilder AddBranch(string name, PathString path, Type startupType)
        {
            Branches.Add(new Branch(name, path, startupType));
            return this;
        }
    }

    public struct Branch
    {
        public Branch(string name, PathString path, Type startupType)
        {
            Name = name;
            Path = path;
            StartupType = startupType;
        }
        
        public string Name { get;  }
        public PathString Path { get; }
        public Type StartupType { get; }
    }

    public static class ParallelPipelinesExtensions
    {
        /// <summary>
        /// Sets up an application branch with an isolated DI container
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="branchName">Pipeline name</param>
        /// <param name="path">Relative path for the application branch</param>
        /// <param name="servicesConfiguration">DI container configuration</param>
        /// <param name="appBuilderConfiguration">Application pipeline configuration for the created branch</param>
        public static IApplicationBuilder UseBranch(this IApplicationBuilder app, string branchName, PathString path, 
            Action<IServiceCollection> servicesConfiguration, Action<IApplicationBuilder> appBuilderConfiguration)
        {
            return app.UseBranch(branchName, new[] { path }, servicesConfiguration, appBuilderConfiguration);
        }

        /// <summary>
        /// Sets up an application branch with an isolated DI container with several routes (entry points)
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="branchName">Pipeline name</param>
        /// <param name="paths">Relative paths for the application branch</param>
        /// <param name="servicesConfiguration">DI container configuration</param>
        /// <param name="appBuilderConfiguration">Application pipeline configuration for the created branch</param>
        public static IApplicationBuilder UseBranch(this IApplicationBuilder app, string branchName, IEnumerable<PathString> paths,
            Action<IServiceCollection> servicesConfiguration, Action<IApplicationBuilder> appBuilderConfiguration)
        {
            var applicationServices = app.ApplicationServices;

            var webHost = new WebHostBuilder()
                .ConfigureServices(s => {
                    s.AddSingleton<IServer, DummyServer>();
                    s.AddSingleton<IPipelineIdentity>(_ => new PipelineIdentity(branchName, paths));
                })
                .ConfigureServices(servicesConfiguration)
                .UseStartup<EmptyStartup>()
                .Build();
            
            var serviceProvider = webHost.Services;
            var serverFeatures = webHost.ServerFeatures;
            
            var appBuilderFactory = serviceProvider.GetRequiredService<IApplicationBuilderFactory>();
            var branchBuilder = appBuilderFactory.CreateBuilder(serverFeatures);
            var factory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            branchBuilder.Use(async (context, next) =>
            {
                var coreServiceProvider = context.RequestServices;
                using (var scope = factory.CreateScope())
                {
                    context.RequestServices = scope.ServiceProvider;
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
}