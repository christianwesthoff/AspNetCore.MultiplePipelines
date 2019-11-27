using System.Threading.Tasks;
using AspNetCore.MultiplePipelines.Extensions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AspNetCore.MultiplePipelines.Example.Mvc
{
    public class Test
    {
        public string Content { get; }

        public Test(string content)
        {
            Content = content;
        }
    }
    
    public class TestConsumer: IConsumer<Test>
    {
        private readonly ILogger<TestConsumer> _logger;
        private readonly IPipelineIdentity _identity;

        public TestConsumer(ILogger<TestConsumer> logger, IPipelineIdentity identity)
        {
            _logger = logger;
            _identity = identity;
        }
        
        public async Task Consume(ConsumeContext<Test> context)
        {
            _logger.LogCritical($"[{_identity.Name}] {context.Message.Content}");
            await Task.CompletedTask;
        }
    }
}