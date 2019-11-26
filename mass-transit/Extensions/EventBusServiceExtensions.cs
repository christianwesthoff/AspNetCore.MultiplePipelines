using System;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace mass_transit.Extensions
{

    public static class EventBusServiceExtensions
    {

        public static IServiceCollection AddEventBus(this IServiceCollection services, params Assembly[] assemblies)
        {
            services.AddMassTransit(cfg =>
            {
                cfg.AddBus(ConfigureBus);
            });
            services.AddMassTransitHostedService();
            return services;
        }

        private static IBusControl ConfigureBus(IServiceProvider provider)
        {
            return Bus.Factory.CreateUsingInMemory(cfg => { cfg.ReceiveEndpoint("queue", ep =>
            {
            }); });
        }
    }
}