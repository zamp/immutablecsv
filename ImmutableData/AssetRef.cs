using System;
using Object = UnityEngine.Object;

namespace ImmutableData
{
	[Serializable]
	[Obsolete("Do not use directly in Immutables! Use generic AssetRef<T> instead!")]
	public abstract class AssetRef
	{
		/// <summary>
		/// Gets the type
		/// </summary>
		public virtual Type AssetType { get; } = null;
		
		/// <summary>
		/// Gets the asset guid
		/// </summary>
		public string Name { get; internal set; }
	}
	
	[Serializable]
#pragma warning disable CS0618 // Type or member is obsolete
	public class AssetRef<T> : AssetRef where T : Object
#pragma warning restore CS0618 // Type or member is obsolete
	{
		/// <summary>
		/// Gets the type
		/// </summary>
		public override Type AssetType => typeof(T);
	}
}