#nullable enable

using System;

namespace ImmutableData
{
	/// <summary>
	/// Resolves an asset reference to the host's asset object. Hosts (a Unity project, a Godot game, a tool) set
	/// <see cref="AssetRef.Resolver"/>; the data library itself never loads assets.
	/// </summary>
	public interface IAssetResolver
	{
		object? Resolve(Type assetType, string name);
	}

	/// <summary>A reference to an asset: a name plus the asset type the field expects. No engine types involved.</summary>
	[Serializable]
	[Obsolete("Do not use directly in Immutables! Use generic AssetRef<T> instead!")]
	public abstract class AssetRef
	{
		/// <summary>Set by the host to load assets by name.</summary>
		public static IAssetResolver? Resolver { get; set; }

		/// <summary>
		/// Gets the type
		/// </summary>
		public virtual Type AssetType { get; } = typeof(object);

		/// <summary>
		/// Gets the asset name (or path), as written in the CSV
		/// </summary>
		public string Name { get; internal set; } = "";
	}

	[Serializable]
#pragma warning disable CS0618 // Type or member is obsolete
	public class AssetRef<T> : AssetRef where T : class
#pragma warning restore CS0618 // Type or member is obsolete
	{
		/// <summary>
		/// Gets the type
		/// </summary>
		public override Type AssetType => typeof(T);

		/// <summary>Loads the asset through <see cref="AssetRef.Resolver"/>. Null when no resolver is set or the asset is missing.</summary>
		public T? Get()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return Resolver?.Resolve(typeof(T), Name) as T;
#pragma warning restore CS0618 // Type or member is obsolete
		}
	}
}
