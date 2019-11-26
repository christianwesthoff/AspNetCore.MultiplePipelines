using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;

namespace mass_transit.Extensions
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
            return services;
        }

        public interface IBusConfiguration
        {
            IDictionary<Assembly, Func<IServiceProvider, Type, IConsumer>> ConsumerConfigurations { get; }
        }
        
        private class DefaultBusConfiguration : IBusConfiguration
        {
            public IDictionary<Assembly, Func<IServiceProvider, Type, IConsumer>> ConsumerConfigurations { get; } = 
                new Dictionary<Assembly, Func<IServiceProvider, Type, IConsumer>>();
        }

        private static IBusControl ConfigureBus(IServiceProvider provider, IBusConfiguration configuration)
        {
            return Bus.Factory.CreateUsingInMemory(cfg => 
            {
                cfg.ReceiveEndpoint("queue", ep =>
                {
                    configuration.ConsumerConfigurations.ForEach((assembly, configure) =>
                    {
                        var consumerTypes = assembly.FindDerivedTypes(typeof(IConsumer));
                        consumerTypes.ForEach(consumerType => ep.Consumer(consumerType, type => configure(provider, type)));
                    });
                });
            });
        }
        
        public static void ForEach<T, TE>(this IDictionary<T, TE> source, Action<T, TE> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            foreach (var element in source)
                action(element.Key, element.Value);
        }
    }
}