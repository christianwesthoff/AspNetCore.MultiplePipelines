using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;

namespace AspNetCore.ParallelPipelines.Extensions
{

    public static class EventBusServiceExtensions
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services, Action<IBusConfiguration> configure = null)
        {
            services.AddMassTransit(cfg =>
            {
                var busConfiguration = new DefaultBusConfiguration();
                configure?.Invoke(busConfiguration);
                cfg.AddBus(provider => ConfigureBus(provider, busConfiguration));
            });
            services.AddMassTransitHostedService();
            // Hotfix mass transit
            services.Remove(services.FirstOrDefault(t => t.ServiceType == typeof(IPublishEndpoint) && t.Lifetime == ServiceLifetime.Scoped));
            return services;
        }

        private class DefaultBusConfiguration : IBusConfiguration
        {
            public IDictionary<string, ConsumerConfiguration> ConsumerConfigurations { get; } = 
                new Dictionary<string, ConsumerConfiguration>();
        }

        private static IBusControl ConfigureBus(IServiceProvider provider, IBusConfiguration configuration)
        {
            return Bus.Factory.CreateUsingInMemory(cfg => 
            {
                cfg.ReceiveEndpoint("queue", ep =>
                {
                    configuration.ConsumerConfigurations.ForEach((name, consumerConfiguration) =>
                    {
                        var consumerTypes = consumerConfiguration.Assembly.FindDerivedTypes(typeof(IConsumer));
                        consumerTypes.ForEach(consumerType => ep.Consumer(consumerType, type => consumerConfiguration.Configure(provider, type)));
                    });
                });
            });
        }
    }
    
    public interface IBusConfiguration
    {
        IDictionary<string, ConsumerConfiguration> ConsumerConfigurations { get; }
    }
        
    public class ConsumerConfiguration
    {
        public ConsumerConfiguration(Assembly assembly, Func<IServiceProvider, Type, IConsumer> configure)
        {
            Assembly = assembly;
            Configure = configure;
        }
            
        public Assembly Assembly { get; }
        public Func<IServiceProvider, Type, IConsumer> Configure { get; }
    }
}