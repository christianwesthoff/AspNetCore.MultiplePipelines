using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoreLinq;

namespace AspNetCore.MultiplePipelines.Extensions
{
    public static class ParallelPipelinesExtensions
    {
        private static readonly Type[] DefaultSharedTypes =
        {
            typeof(IBusControl),
            typeof(IPublishEndpoint),
            typeof(ISendEndpoint),
            typeof(ILoggerFactory)
        };
        
        public static IWebHostBuilder UseMultiplePipelines(this IWebHostBuilder webHost,
            Action<IMultiplePipelineBuilder> builderConfiguration, params Type[] sharedTypes) =>
            UseMultiplePipelines(webHost,
                builderConfiguration, null, sharedTypes);
        
        public static IWebHostBuilder UseMultiplePipelines(this IWebHostBuilder webHost,
            Action<IMultiplePipelineBuilder> builderConfiguration,
            Action<IServiceCollection> serviceConfiguration = null, params Type[] sharedTypes)
        {
            sharedTypes = sharedTypes.Concat(DefaultSharedTypes).ToArray();
            var pipelineBuilder = new DefaultMultiplePipelineBuilder();
            builderConfiguration.Invoke(pipelineBuilder);
            IServiceCollection sharedServiceCollection = null;
            return webHost.ConfigureServices(services =>
            {
                services.AddServiceProviderBridge();
                serviceConfiguration?.Invoke(services);
                services.AddEventBus(configure =>
                {
                    pipelineBuilder.Branches.ForEach(branch =>
                    {
                        var pipelineAssembly = branch.StartupType.Assembly;
                        configure.ConsumerConfigurations.Add(branch.Name, new ConsumerConfiguration(pipelineAssembly,
                            (provider, type) =>
                            {
                                var bridge = provider
                                    .GetRequiredService<IPipelineServiceProviderBridge>();
                                var pipelineServiceProvider = bridge
                                    .PipelineServiceProviders[branch.Name];
                                using (var pipelineScope = pipelineServiceProvider
                                    .GetRequiredService<IServiceScopeFactory>().CreateScope())
                                {
                                    return (IConsumer) pipelineScope.ServiceProvider.GetService(type);
                                }
                            }));
                    });
                });
                sharedServiceCollection = services;
            }).Configure((context, builder) =>
            {
                pipelineBuilder.Branches.ForEach(branch =>
                {
                    var startup = StartupLoader.LoadMethods(builder.ApplicationServices, branch.StartupType,
                        context.HostingEnvironment.EnvironmentName);
                    builder.UseBranch(branch.Name, branch.Path, (services) =>
                        {
                            branch.StartupType.Assembly.FindDerivedTypes(typeof(IConsumer))
                                .ForEach(consumerType => services.AddScoped(consumerType));
                            
                            startup.ConfigureServicesDelegate(services);
                            
                            sharedServiceCollection.Where(service => sharedTypes.Contains(service.ServiceType)).ForEach(service => 
                                services.Add(new ServiceDescriptor(service.ServiceType, 
                                    _ => builder.ApplicationServices.GetService(service.ServiceType), 
                                    service.Lifetime)));
                            
                        },
                    startup.ConfigureDelegate);
                });
            });
        }

        private class DefaultMultiplePipelineBuilder : IMultiplePipelineBuilder
        {
            public ICollection<Branch> Branches { get; } = new List<Branch>();

            public IMultiplePipelineBuilder UseBranch<T>(string name, PathString path)
            {
                return UseBranch(name, path, typeof(T));
            }

            public IMultiplePipelineBuilder UseBranch(string name, PathString path, Type startupType)
            {
                Branches.Add(new Branch(name, path, startupType));
                return this;
            }
        }

        
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

            var bridge = app.ApplicationServices.GetRequiredService<IPipelineServiceProviderBridge>();
            bridge.PipelineServiceProviders.Add(branchName, serviceProvider);
            
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
    
    public interface IPipelineServiceProviderBridge
    {
        IDictionary<string, IServiceProvider> PipelineServiceProviders { get; }
    }

    public static class ParallelServiceProviderBridgeServiceExtension
    {
        public static IServiceCollection AddServiceProviderBridge(this IServiceCollection services)
        {
            services.AddSingleton<IPipelineServiceProviderBridge, PipelineServiceProviderBridge>();
            return services;
        }
        
        private class PipelineServiceProviderBridge : IPipelineServiceProviderBridge
        {
            public IDictionary<string, IServiceProvider> PipelineServiceProviders { get; } = new Dictionary<string, IServiceProvider>();
        }
    }


    public interface IMultiplePipelineBuilder
    {
        IMultiplePipelineBuilder UseBranch<T>(string name, PathString path);
        IMultiplePipelineBuilder UseBranch(string name, PathString path, Type startupType);
    }
    
    public sealed class Branch
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
}