#nullable enable

using System.Collections;
using System.Reflection;

namespace ImmutableData
{
	/// <summary>
	/// Checks what was read into an immutable: a field that is a non-nullable reference must be set (a left-empty
	/// required cell), and so must every such [Order] field of the [Ordered] values in it (a requirement written as a
	/// bare class name); nested values are checked the same way and an <see cref="IValidatable"/> value checks itself.
	/// Errors name the field path.
	/// </summary>
	public static class OrderedValues
	{
		private static readonly NullabilityInfoContext Nullability = new();

		/// <summary>Checks the owner's public fields: a non-nullable reference must be set, and every [Ordered] value in them is checked.</summary>
		public static bool Validate(object owner, Result result)
		{
			foreach (var field in owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
			{
				var value = field.GetValue(owner);

				// the name and recycle-bin fields belong to the database, which checks them itself
				if (IsMissing(field, value) && field.DeclaringType != typeof(Immutable))
					result.AddError($"{field.Name} is required.");

				ValidateValue(value, field.Name, result);
			}

			return result.IsSuccess;
		}

		/// <summary>Checks one value, a list of values, or nothing when it holds no [Ordered] value.</summary>
		public static bool ValidateValue(object? value, string path, Result result)
		{
			switch (value)
			{
				case null:
				case string:
				case Immutable:
					return result.IsSuccess;
				case IEnumerable list:
					var index = 0;
					foreach (var item in list)
						ValidateValue(item, $"{path}[{index++}]", result);

					return result.IsSuccess;
			}

			var type = value.GetType();
			if (!type.IsDefined(typeof(OrderedAttribute), true))
				return result.IsSuccess;

			var name = $"{path} {type.Name}";

			foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!field.IsDefined(typeof(OrderAttribute), false))
					continue;

				var fieldValue = field.GetValue(value);

				if (IsMissing(field, fieldValue))
					result.AddError($"{name}: {field.Name} is required.");

				ValidateValue(fieldValue, $"{name}.{field.Name}", result);
			}

			if (value is IValidatable validatable)
			{
				var before = result.Errors.Count;
				validatable.Validate(result);
				result.PrefixErrors(before, $"{name}: ");
			}

			return result.IsSuccess;
		}

		private static bool IsMissing(FieldInfo field, object? value)
		{
			return value == null && !field.FieldType.IsValueType && Nullability.Create(field).ReadState == NullabilityState.NotNull;
		}
	}
}
