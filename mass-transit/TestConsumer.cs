using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;

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
        public async Task Consume(ConsumeContext<Test> context)
        {
            Debug.WriteLine(context.Message.Content);
            await Task.CompletedTask;
        }
    }
}