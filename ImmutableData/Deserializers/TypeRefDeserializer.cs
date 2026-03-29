using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Deserializers
{
    public class TypeRefDeserializer : IImmutableDeserializer
    {
	    private static readonly List<Type> _matches = new();

	    public bool CanDeserialize(Type type)
        {
            return typeof(TypeRef).IsAssignableFrom(type);
        }

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
            var typeName = data;

            if (TryGetType(typeName, out var assemblyType))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TypeRef<>))
                {
                    deserializedObject = Activator.CreateInstance(type, assemblyType);
                }
                else
                {
                    deserializedObject = new TypeRef(assemblyType);
                }
                return true;
            }

            deserializedObject = null;
            return false;
        }

        internal static bool TryGetType(string typeName, out Type type)
        {
	        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
	        _matches.Clear();
	        
            foreach (var assembly in assemblies)
            {
                type = null;

                if (assembly == null || string.IsNullOrEmpty(typeName))
	                continue;

                if (assembly.IsDynamic)
	                continue;

                type = assembly.GetType(typeName, false, false);
                if (type != null)
	                return true;
                
                type = assembly.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (type != null)
                {
	                _matches.Add(type);
                }
            }

            #if UNITY_5_3_OR_NEWER
            if (_matches.Count > 1)
            {
	            UnityEngine.Debug.LogWarning($"More than one type found for name: {typeName}");
            }
            #endif
            if (_matches.Count > 0)
            {
	            type = _matches[0];
	            return true;
            }

            type = null;
            return false;
        }
    }
}