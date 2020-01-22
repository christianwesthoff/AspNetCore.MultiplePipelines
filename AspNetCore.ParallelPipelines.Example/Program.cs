using AspNetCore.ParallelPipelines.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.ParallelPipelines.Example
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
                                builder.UseBranch<Test.Startup>("endpoint", "/api");
                                builder.UseBranch<Test2.Startup>("endpoint2", "/api2");
                            });
                    });
    }
}