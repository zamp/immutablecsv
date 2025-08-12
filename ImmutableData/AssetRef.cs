using System;
using Object = UnityEngine.Object;

namespace ImmutableData
{
	[Serializable]
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
	public class AssetRef<T> : AssetRef where T : Object
	{
		/// <summary>
		/// Gets the type
		/// </summary>
		public override Type AssetType => typeof(T);
	}
}