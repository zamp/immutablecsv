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

	[Serializable]
	public class TypeRef<T> : TypeRef where T : class
	{
		public TypeRef(Type type) : base(type)
		{
		}

		public T CreateInstance() => (T)Activator.CreateInstance(Type);
	}
}