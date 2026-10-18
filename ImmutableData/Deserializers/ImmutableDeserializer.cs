using System;
using System.Linq;

namespace ImmutableData.Deserializers
{
	public class ImmutableDeserializer : IImmutableDeserializer
	{
		public static ImmutableDatabase ImmutableDatabase;

		/// <summary>
		/// Where the active row's cell being read is, while the importer reads one; null otherwise. Active data may not
		/// name a retired immutable, which is kept only for old saves and other retired rows, and is warned when it names
		/// one by a name the recycle bin renamed.
		/// </summary>
		public static string ActiveCell;

		public bool CanDeserialize(Type type)
		{
			return typeof(Immutable).IsAssignableFrom(type) || type.IsDefined(typeof(ImmutableReferenceAttribute), true);
		}

		public bool Deserialize(Type type, string data, out object deserializedObject)
		{
			if (ImmutableDatabase.TryGet(type, data, out var found))
			{
				if (ActiveCell != null && found.IsRetired)
					throw new InvalidOperationException($"names the retired {ImmutableDatabase.GetRootBaseType(found.GetType()).Name} '{found.Name}', which is kept only for old saves; name an active one instead");

				if (ActiveCell != null && found.Name != data.Trim())
					ImmutableDatabase.AddWarning($"{ActiveCell}: names '{data.Trim()}', which the recycle bin renamed to '{found.Name}'; name '{found.Name}' instead.");

				deserializedObject = found;
				return true;
			}

			if (type.IsDefined(typeof(ImmutableReferenceAttribute), true))
			{
				var matches = ImmutableDatabase.GetAll<Immutable>().Where(x => x.Name == data && type.IsInstanceOfType(x)).Take(2).ToArray();

				deserializedObject = matches.Length == 1 ? matches[0] : null;
				return matches.Length == 1;
			}

			ImmutableImporter.AddBreadcrumb("Immutable " + type.Name + " not found with data: " + data);
			deserializedObject = null;
			return false;
		}
	}
}
