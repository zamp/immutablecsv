using System;

namespace ImmutableData.Deserializers
{
    public class AssetRefDeserializer : IImmutableDeserializer
    {
        public bool CanDeserialize(Type type)
        {
#pragma warning disable CS0618 // Type or member is obsolete
	        return typeof(AssetRef).IsAssignableFrom(type);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public bool Deserialize(Type type, string data, out object value)
        {
            if (string.IsNullOrEmpty(data))
            {
                value = null;
                return true;
            }
            
#pragma warning disable CS0618 // Type or member is obsolete
            var assetRef = (AssetRef)Activator.CreateInstance(type);
#pragma warning restore CS0618 // Type or member is obsolete
            assetRef.Name = data;
            value = assetRef;
            return true;
        }
    }
}