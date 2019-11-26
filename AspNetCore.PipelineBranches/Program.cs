using AspNetCore.PipelineBranches.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

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
                        webBuilder.UseKestrel().ConfigureMultiplePipelines(builder =>
                            {
                                builder.AddBranch<Startup>("api1", "/api1");
                                builder.AddBranch<Startup>("api2", "/api2");
                            });
                    });
    }
}