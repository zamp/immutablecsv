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
	        if (ImmutableDatabase.Exists(type, data))
	        {
		        deserializedObject = ImmutableDatabase.Get(type, data);
		        return true;
	        }

            ImmutableImporter.Breadcrumbs.Add("Immutable " + type.Name + " not found with data: " + data);
            deserializedObject = null;
            return false;
        }
    }
}