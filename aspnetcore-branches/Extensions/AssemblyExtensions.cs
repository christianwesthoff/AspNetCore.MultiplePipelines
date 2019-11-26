using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace mass_transit.Extensions
{
    public static class AssemblyExtension
    {
        public static IEnumerable<Type> FindDerivedTypes(this Assembly assembly, Type baseType)
        {
            return assembly.GetTypes().Where(t => t != baseType && baseType.IsAssignableFrom(t));
        }
    }
}