#nullable enable

using System.Reflection;

namespace ImmutableData.Extensions
{
	public static class ReflectionExtensions
	{
		/// <summary>The compiler-generated backing field of an auto-property, so get-only properties can be filled.</summary>
		public static FieldInfo GetBackingField(this PropertyInfo property)
		{
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var name = $"<{property.Name}>k__BackingField";

			for (var type = property.DeclaringType; type != null; type = type.BaseType)
			{
				var field = type.GetField(name, flags);
				if (field != null)
					return field;
			}

			throw new System.MissingFieldException($"{property.DeclaringType?.Name}.{property.Name} has no setter and is not an auto-property.");
		}
	}
}
