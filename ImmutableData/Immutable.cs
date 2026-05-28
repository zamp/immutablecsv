#nullable enable

using System;

namespace ImmutableData
{
	public class Immutable : IComparable<Immutable>
	{
		public readonly string Name = null!;

		public int CompareTo(Immutable other)
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