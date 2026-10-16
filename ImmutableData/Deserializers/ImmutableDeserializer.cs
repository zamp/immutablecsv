using System;
using System.Linq;

namespace ImmutableData.Deserializers
{
    public class ImmutableDeserializer : IImmutableDeserializer
    {
        public static ImmutableDatabase ImmutableDatabase;
        
        public bool CanDeserialize(Type type)
        {
	        return typeof(Immutable).IsAssignableFrom(type)
	               || type.IsDefined(typeof(ImmutableReferenceAttribute), true);
        }

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
	        if (ImmutableDatabase.Exists(type, data))
	        {
		        deserializedObject = ImmutableDatabase.Get(type, data);
		        return true;
	        }

	        if (type.IsDefined(typeof(ImmutableReferenceAttribute), true))
	        {
		        var matches = ImmutableDatabase.GetAll<Immutable>()
		        	.Where(x => x.Name == data && type.IsInstanceOfType(x))
		        	.Take(2)
		        	.ToArray();

		        deserializedObject = matches.Length == 1 ? matches[0] : null;
		        return matches.Length == 1;
	        }

            ImmutableImporter.AddBreadcrumb("Immutable " + type.Name + " not found with data: " + data);
            deserializedObject = null;
            return false;
        }
    }
}
