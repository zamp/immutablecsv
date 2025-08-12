using System;

namespace ImmutableData.Deserializers
{
	public interface IImmutableDeserializer
	{
		/// <summary>
		/// Can deserialize provided type
		/// </summary>
		bool CanDeserialize(Type type);
		
		/// <summary>
		/// Deserialize data.
		/// This method can be called multiple times.
		/// </summary>
		bool Deserialize(Type type, string data, out object deserializedObject);
	}
}