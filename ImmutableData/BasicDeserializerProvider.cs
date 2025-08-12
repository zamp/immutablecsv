using System.Collections.Generic;
using ImmutableData.Deserializers;

namespace ImmutableData
{
    public class BasicDeserializerProvider : IDeserializerProvider
    {
        public IEnumerable<IImmutableDeserializer> Deserializers => new List<IImmutableDeserializer>()
        {
            // Ordered with most likely to be used first
            new ValueTypesDeserializer(),
            new ImmutableDeserializer(),
            new OrderedTypeDeserializer(),
            new AssetRefDeserializer(),
            new TimeSpanDeserializer(),
            new TypeRefDeserializer()
        };
    }
}