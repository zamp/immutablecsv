using System.Collections.Generic;
using ImmutableData.Deserializers;

namespace ImmutableData
{
    public interface IDeserializerProvider
    {
        IEnumerable<IImmutableDeserializer> Deserializers { get; }
    }
}