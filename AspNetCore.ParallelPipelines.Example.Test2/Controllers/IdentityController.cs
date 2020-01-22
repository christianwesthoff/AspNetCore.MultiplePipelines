using System.Threading;
using System.Threading.Tasks;
using AspNetCore.ParallelPipelines.Example.Events;
using AspNetCore.ParallelPipelines.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AspNetCore.ParallelPipelines.Example.Test2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IdentityController : ControllerBase
    {
        private readonly ILogger<IdentityController> _logger;
        private readonly IPublishEndpoint _busManager;
        private readonly IPipelineIdentity _identity;

        public IdentityController(ILogger<IdentityController> logger, IPublishEndpoint busManager, IPipelineIdentity identity)
        {
            _logger = logger;
            _busManager = busManager;
            _identity = identity;
        }

        [HttpGet]
        public async Task<IPipelineIdentity> Get(CancellationToken cancellationToken)
        {
            await _busManager.Publish(new Test($"Hallo from {_identity.Name} (\"{string.Join(',', _identity.Paths)}\")!"), cancellationToken);
            return _identity;
        }
    }
}