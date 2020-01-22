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
                                builder.UseBranch<Test.Startup>("test", "/test");
                                builder.UseBranch<Test2.Startup>("test2", "/test2");
                                // builder.UseBranch<Mvc.Startup>("default", "");
                            });
                    });
    }
}