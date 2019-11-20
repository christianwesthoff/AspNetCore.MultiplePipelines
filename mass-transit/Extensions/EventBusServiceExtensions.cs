using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mass_transit.Extensions
{

    public static class EventBusServiceExtensions
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services)
        {
            services.AddMassTransit(cfg =>
            {
                cfg.AddBus(ConfigureBus);
            });
            services.AddHostedService<MassTransitHostedService>();
            return services;
        }
        

        public static IServiceCollection AddEventBusConsumers(this IServiceCollection services, Assembly assembly)
        {
            return services;
        }
        
        private static IBusControl ConfigureBus(IServiceProvider provider)
        {
            return Bus.Factory.CreateUsingInMemory(bus =>
            {
                bus.ConfigureEndpoints(provider);
            });
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class MassTransitHostedService :
            IHostedService
        {
            readonly IBusControl _bus;
            private readonly ILogger<MassTransitHostedService> _logger;

            public MassTransitHostedService(IBusControl bus, ILogger<MassTransitHostedService> logger)
            {
                _bus = bus;
                _logger = logger;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                await _bus.StartAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Event bus started.");
                
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await _bus.StopAsync(cancellationToken);
                _logger.LogDebug("Event bus stopped.");
            }
        }
    }
}