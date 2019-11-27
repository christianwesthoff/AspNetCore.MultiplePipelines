using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.MultiplePipelines.Example.Mvc;
using AspNetCore.MultiplePipelines.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AspNetCore.MultiplePipelines.Example.Mvc.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IBusControl _busManager;
        private readonly IPipelineIdentity _identity;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IBusControl busManager, IPipelineIdentity identity)
        {
            _logger = logger;
            _busManager = busManager;
            _identity = identity;
        }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get(CancellationToken cancellationToken)
        {
            await _busManager.Publish(new Test($"Hallo from {_identity.Name} (\"{string.Join(',', _identity.Paths)}\")!"), cancellationToken);
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(index),
                    TemperatureC = rng.Next(-20, 55),
                    Summary = Summaries[rng.Next(Summaries.Length)]
                })
                .ToArray();
        }
    }
}