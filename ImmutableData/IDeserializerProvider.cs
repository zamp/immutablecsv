using System.Collections.Generic;
using ImmutableData.Deserializers;

namespace ImmutableData
{
    public interface IDeserializerProvider
    {
	    IEnumerable<IDeserializerProvider> Providers { get; }
        IEnumerable<IImmutableDeserializer> Deserializers { get; }
    }
}