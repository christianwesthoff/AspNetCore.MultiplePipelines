using AspNetCore.MultiplePipelines.Example.Mvc;
using AspNetCore.MultiplePipelines.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.MultiplePipelines.Example
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
                        webBuilder.UseKestrel()
                            .UseMultiplePipelines(builder =>
                            {
                                builder.UseBranch<Startup>("api1", "/api");
                                builder.UseBranch<Startup>("api2", "/api1");
                                builder.UseBranch<Startup>("default", "");
                            });
                    });
    }
}