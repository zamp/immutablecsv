using System;

namespace ImmutableData.Deserializers
{
    public class ImmutableDeserializer : IImmutableDeserializer
    {
        public static ImmutableDatabase ImmutableDatabase;
        
        public bool CanDeserialize(Type type)
        {
            return typeof(Immutable).IsAssignableFrom(type);
        }

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
            var model = ImmutableDatabase.Get(type, data);
            if (model != null)
            {
                deserializedObject = model;
                return true;
            }

            ImmutableImporter.Breadcrumbs.Add("Failed to deserialize " + type.Name + " with data " + data);
            deserializedObject = null;
            return false;
        }
    }
}