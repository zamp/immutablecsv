#nullable enable

using System;

namespace ImmutableData
{
	public class Immutable : IComparable<Immutable>
	{
		public readonly string Name = null!;

		/// <summary>
		/// True for an immutable loaded from a recycle bin. Retired immutables can still be fetched by name, so old saves
		/// load, but <see cref="ImmutableDatabase.GetAll{T}"/> never returns them.
		/// </summary>
		public bool IsRetired { get; internal set; }

		/// <summary>The version a retired immutable was removed in, when its recycle-bin row says so.</summary>
		public string? RemovedIn { get; internal set; }

		public int CompareTo(Immutable? other)
		{
			if (ReferenceEquals(this, other)) return 0;
			if (ReferenceEquals(null, other)) return 1;
			return string.Compare(Name, other.Name, StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return $"{GetType().Name}:{Name}";
		}

		public virtual bool Validate(Result result)
		{
			return result.IsSuccess;
		}

		public virtual void PostImport(ImmutableDatabase database)
		{
		}

		public virtual void PostImport<T>(ImmutableDatabase database, T gameState)
		{
		}

		public virtual void PrePostImport(ImmutableDatabase immutableDatabase)
		{
		}
	}
}
