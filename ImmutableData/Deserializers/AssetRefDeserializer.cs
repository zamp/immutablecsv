using System;

namespace ImmutableData.Deserializers
{
    public class AssetRefDeserializer : IImmutableDeserializer
    {
        public bool CanDeserialize(Type type)
        {
            return typeof(AssetRef).IsAssignableFrom(type);
        }

        public bool Deserialize(Type type, string data, out object value)
        {
            if (string.IsNullOrEmpty(data))
            {
                value = null;
                return true;
            }
            
            var assetRef = (AssetRef)Activator.CreateInstance(type);
            assetRef.Name = data;
            value = assetRef;
            return true;
        }
    }
}