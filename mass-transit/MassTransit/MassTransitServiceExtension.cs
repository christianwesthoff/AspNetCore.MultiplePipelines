using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Extensions.Hosting;
using MassTransit.Extensions.Hosting.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace mass_transit.MassTransit
{
    public static class MassTransitServiceExtension
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services)
        {
            services.AddMassTransit(busBuilder =>
            {
                busBuilder.UseHttp("connection-name-1", new Uri("http://localhost:3333"), hostBuilder =>
                {
                    hostBuilder.UseServiceScope();
                    hostBuilder.UseMethod(HttpMethod.Post);
                    hostBuilder.AddReceiveEndpoint("example-queue-1", endpointBuilder =>
                    {
                        endpointBuilder.AddConfigurator(cfg => cfg.);
                        endpointBuilder.AddConsumer<TestConsumer>();
                    });
                });
            });
            services.AddHostedService<BusService>();
            return services;
        }
        
        private class BusService : IHostedService
        {
            private readonly IBusControl _busControl;

            public BusService(IBusControl busControl)
            {
                _busControl = busControl;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return _busControl.StartAsync(cancellationToken);
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return _busControl.StopAsync(cancellationToken);
            }
        }
    }

    public class TestConsumer : IConsumer<string>
    {
        public Task Consume(ConsumeContext<string> context)
        {
            Debug.WriteLine(context.Message);
            return Task.CompletedTask;
        }
    }
}