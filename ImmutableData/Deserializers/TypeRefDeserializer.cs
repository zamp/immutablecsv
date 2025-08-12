using System;
using System.Linq;

namespace ImmutableData.Deserializers
{
    public class TypeRefDeserializer : IImmutableDeserializer
    {
        public bool CanDeserialize(Type type)
        {
            return typeof(TypeRef).IsAssignableFrom(type);
        }

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
            var typeName = data;

            if (TryGetType(typeName, out var assemblyType))
            {
                deserializedObject = new TypeRef(assemblyType);
                return true;
            }
            
            deserializedObject = null;
            return false;
        }

        internal static bool TryGetType(string typeName, out Type type)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = null;

                if (assembly == null || string.IsNullOrEmpty(typeName))
                    return false;

                if (assembly.IsDynamic)
                    return false;

                type = assembly.GetType(typeName, false, false);

                return type != null;
            }

            type = null;
            return false;
        }
    }
}