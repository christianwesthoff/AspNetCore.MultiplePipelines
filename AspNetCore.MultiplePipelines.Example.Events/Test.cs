using System;

namespace AspNetCore.MultiplePipelines.Example.Events
{
    public class Test
    {
        public string Content { get; }

        public Test(string content)
        {
            Content = content;
        }
    }
}