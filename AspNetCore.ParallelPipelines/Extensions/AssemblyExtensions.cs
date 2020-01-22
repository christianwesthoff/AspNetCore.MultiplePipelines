using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AspNetCore.ParallelPipelines.Extensions
{
    public static class AssemblyExtension
    {
        public static IEnumerable<Type> FindDerivedTypes(this Assembly assembly, Type baseType)
        {
            return assembly.GetTypes().Where(t => t != baseType && baseType.IsAssignableFrom(t));
        }
    }
}