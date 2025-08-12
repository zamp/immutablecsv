using System;

namespace ImmutableData
{
	[Serializable]
	public class TypeRef
	{
		/// <summary>
		/// Gets the type
		/// </summary>
		public Type Type { get; internal set; }

		public TypeRef(Type type)
		{
			Type = type;
		}
	}
}