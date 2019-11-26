using System.Diagnostics;
using System.Threading.Tasks;
using mass_transit.Extensions;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.Logging;

namespace mass_transit
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

        public TestConsumer(ILogger<TestConsumer> logger)
        {
            _logger = logger;
        }
        
        public async Task Consume(ConsumeContext<Test> context)
        {
            _logger.LogDebug(context.Message.Content);
            await Task.CompletedTask;
        }
    }
}