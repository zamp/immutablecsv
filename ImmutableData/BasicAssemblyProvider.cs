using System.Collections.Generic;
using System.Reflection;

namespace ImmutableData
{
    public class BasicAssemblyProvider : IAssemblyProvider
    {
        public IEnumerable<Assembly> Assemblies => new List<Assembly>()
        {
            typeof(Immutable).Assembly
        };
    }
}