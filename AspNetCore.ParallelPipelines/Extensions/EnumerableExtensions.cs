using System;
using System.Collections.Generic;

namespace AspNetCore.ParallelPipelines.Extensions
{
    public static class EnumerableExtensions
    {
        public static void ForEach<T, TE>(this IDictionary<T, TE> source, Action<T, TE> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            foreach (var element in source)
                action(element.Key, element.Value);
        }
    }
}