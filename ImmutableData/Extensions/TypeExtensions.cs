using System;

namespace ImmutableData.Extensions
{
	public static class TypeExtensions
	{
		/// <summary>
		/// Recursively finds out if givenType is assignable to genericType
		/// Works with lists
		/// </summary>
		/// <param name="givenType">Type to check for</param>
		/// <param name="genericType"></param>
		/// <returns></returns>
		public static bool IsAssignableFromRecursive(this Type genericType, Type givenType)
		{
			// pretty sure this can all be replaced with Type.IsAssigableFrom method -zamp
			var interfaceTypes = givenType.GetInterfaces();
            
			if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
				return true;

			foreach (var it in interfaceTypes)
				if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
					return true;
            
			var baseType = givenType.BaseType;
			if (baseType == null) 
				return false;

			return IsAssignableFromRecursive(baseType, genericType);
		}
		
		/// <summary>
		/// Returns true if type is nullable
		/// </summary>
		/// <param name="type">Type to check</param>
		/// <returns>True if type is nullable</returns>
		public static bool IsNullable(this Type type)
		{
			return Nullable.GetUnderlyingType(type) != null;
		}
	}
}