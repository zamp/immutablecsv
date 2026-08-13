using System;

namespace ImmutableData
{
	/// <summary>
	/// Allows a non-Immutable contract to be deserialized by finding a uniquely named
	/// Immutable instance assignable to that contract.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
	public sealed class ImmutableReferenceAttribute : Attribute
	{
	}
}
