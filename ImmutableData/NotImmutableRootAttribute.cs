using System;

namespace ImmutableData
{
	/// <summary>
	/// When applied to an <see cref="Immutable"/>-derived class, this type is ineligible to be chosen
	/// as the root base type when the <see cref="ImmutableDatabase"/> resolves the identifier namespace
	/// for child types: the class just below it is the root instead, and that class's own subclasses share its names.
	///
	/// Use this on shared base classes (e.g. <c>LabeledImmutable</c>) so that unrelated leaf types
	/// which happen to share that base do not collide in the database key.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class NotImmutableRoot : Attribute
	{
	}
}
