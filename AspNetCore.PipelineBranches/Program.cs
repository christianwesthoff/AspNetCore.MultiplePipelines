using AspNetCore.PipelineBranches.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore.PipelineBranches
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel().UseMultiplePipelines(builder =>
                            {
                                builder.UseBranch<Startup>("api1", "/api1");
                                builder.UseBranch<Startup>("api2", "/api2");
                            }, 
                            new [] { 
                                typeof(IBusControl), 
                                typeof(IPublishEndpoint), 
                                typeof(ISendEndpoint), 
                                typeof(ILoggerFactory)
                            });
                    });
    }
}