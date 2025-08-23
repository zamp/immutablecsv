using System.Collections.Generic;
using System.Reflection;

namespace ImmutableData
{
    public interface IAssemblyProvider
    {
        IEnumerable<Assembly> Assemblies { get; }
    }
}