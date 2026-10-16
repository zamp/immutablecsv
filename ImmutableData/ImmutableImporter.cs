#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ImmutableData.Csv;
using ImmutableData.Deserializers;
using ImmutableData.Extensions;
using ImmutableData.Utilities;

namespace ImmutableData
{
	public class ImmutableImporterLog
	{
	}

	/// <summary>An import failure that names where in the data it happened.</summary>
	public class ImmutableImportException : Exception
	{
		public ImmutableImportException(string message, Exception inner = null) : base(message, inner)
		{
		}
	}

	public static class ImmutableImporter
	{
		public static IDeserializerProvider DeserializerProvider = new BasicDeserializerProvider();
		public static IAssemblyProvider AssemblyProvider = null;

		// TODO: move these to an optional configuration file
		public const char ArrayStart = '[';
		public const char ArrayEnd = ']';
		public const char ArrayDelimiter = ',';
		public const char InlineDelimiter = '|';
		public const char TypeDelimiter = ':';
		public const string ClassHeader = "Class";
		public const string KeyHeader = "Key";
		public const string ValueHeader = "Value";
		private const int MaxBreadcrumbs = 100;

		public class ImmutableValidationException : Exception
		{
			public ImmutableValidationException(string message) : base(message)
			{
			}
		}

		private class UnresolvedModel
		{
			public Immutable Immutable;
			public List<Unresolved> Unresolved;
			public ImmutableRow Row;
			public bool Added;
			public string SourceFile => Row.Source;
		}

		private abstract class Unresolved
		{
			public Immutable Immutable;
			public abstract string Name { get; }
			public abstract Type MemberType { get; }
			public string Header;
			public string Value;
		}

		private class UnresolvedField : Unresolved
		{
			public override string Name => FieldInfo.Name;
			public override Type MemberType => FieldInfo.FieldType;
			public FieldInfo FieldInfo;
		}

		private class UnresolvedProperty : Unresolved
		{
			public override string Name => PropertyInfo.Name;
			public override Type MemberType => PropertyInfo.PropertyType;
			public PropertyInfo PropertyInfo;
		}

		public static readonly List<string> Breadcrumbs = new();
		private static readonly List<IImmutableDeserializer> Deserializers = new();

		/// <summary>Imports CSV files. Every file is active data; use <see cref="Import(IEnumerable{ImmutableRow})"/> for recycle bins.</summary>
		public static ImmutableDatabase Import(string[] files)
		{
			var rows = new List<ImmutableRow>();

			foreach (var file in files.Distinct())
			{
				AddBreadcrumb($"Importing file {file}");
				rows.AddRange(CsvRowReader.ReadFile(file));
			}

			return Import(rows);
		}

		/// <summary>
		/// Imports rows read with <see cref="CsvRowReader"/> (possibly merged or filtered by the host). Recycle-bin rows
		/// load as retired immutables, or as renames when they have a <see cref="ImmutableRow.ReplacedBy"/>.
		/// </summary>
		public static ImmutableDatabase Import(IEnumerable<ImmutableRow> rows)
		{
			Breadcrumbs.Clear();
			Deserializers.Clear();
			foreach (var provider in DeserializerProvider.Providers)
			{
				Deserializers.AddRange(provider.Deserializers);
			}

			var db = new ImmutableDatabase();

			try
			{
				var types = GetTypes();
				var models = new List<UnresolvedModel>();

				foreach (var row in rows)
				{
					if (row.Retired && row.ReplacedBy != null)
					{
						AddAlias(db, types, row);
						continue;
					}

					models.Add(ParseRow(types, row));
				}

				return ResolveModels(models, db);
			}
			finally
			{
				ImmutableDeserializer.ImmutableDatabase = null;
			}
		}

		private static void AddAlias(ImmutableDatabase db, List<Type> types, ImmutableRow row)
		{
			var type = FindImmutableType(types, row);

			if (string.IsNullOrEmpty(row.Name))
				throw new ImmutableImportException($"{row.Location}: recycle-bin row with {ImmutableRow.ReplacedByHeader} has no Name.");

			try
			{
				db.AddAlias(type, row.Name, row.ReplacedBy);
			}
			catch (Exception e)
			{
				throw new ImmutableImportException($"{row.Location}: {e.Message}", e);
			}
		}

		/// <summary>
		/// Parses one cell value outside an import, such as a requirement list written in a script, resolving references
		/// against an already loaded database. Throws when the value cannot be read.
		/// </summary>
		public static T Deserialize<T>(string data, ImmutableDatabase database)
		{
			return (T)Deserialize(typeof(T), data, database);
		}

		public static object Deserialize(Type type, string data, ImmutableDatabase database)
		{
			Deserializers.Clear();
			foreach (var provider in DeserializerProvider.Providers)
				Deserializers.AddRange(provider.Deserializers);

			var previous = ImmutableDeserializer.ImmutableDatabase;
			ImmutableDeserializer.ImmutableDatabase = database;

			try
			{
				if (TryDeserialize(type, data ?? "", out var value))
					return value;
			}
			catch (Exception e)
			{
				throw new ImmutableImportException($"'{data}' could not be read as {FriendlyName(type)}: {e.Message}", e);
			}
			finally
			{
				ImmutableDeserializer.ImmutableDatabase = previous;
			}

			throw new ImmutableImportException($"'{data}' could not be read as {FriendlyName(type)}.");
		}

		internal static void AddBreadcrumb(string breadcrumb)
		{
			Breadcrumbs.Add(breadcrumb);

			if (Breadcrumbs.Count > MaxBreadcrumbs)
				Breadcrumbs.RemoveAt(0);
		}

		private static ImmutableDatabase ResolveModels(List<UnresolvedModel> unresolvedModels, ImmutableDatabase db)
		{
			CheckNameConflicts(unresolvedModels);

			ImmutableDeserializer.ImmutableDatabase = db;

			// Resolve in passes until nothing is left. References can point at rows that resolve later, so a pass
			// that makes no progress at all means the rest can never resolve.
			var pending = unresolvedModels;

			while (pending.Count > 0)
			{
				var progress = false;

				foreach (var model in pending)
				{
					if (ResolveUnresolvedModel(model, db))
						progress = true;
				}

				pending = pending.Where(x => !IsResolved(x)).ToList();

				if (pending.Count == 0 || progress)
					continue;

				// Retired rows may point at data that is gone for good; they keep defaults for those members.
				var retired = pending.Where(x => x.Immutable.IsRetired).ToList();
				if (retired.Count > 0)
				{
					foreach (var model in retired)
					{
						foreach (var unresolved in model.Unresolved)
							db.AddWarning($"{model.Row.Location}: retired {model.Immutable} keeps the default {unresolved.Name}; {Describe(unresolved)}.");

						model.Unresolved.Clear();
					}

					pending = pending.Where(x => !IsResolved(x)).ToList();
					continue;
				}

				var error = new StringBuilder();
				error.AppendLine("Could not resolve these values:");

				foreach (var model in pending)
				{
					foreach (var unresolved in model.Unresolved)
						error.AppendLine($"{model.Row.Location}: {model.Immutable}, column {unresolved.Header}: {Describe(unresolved)}");
				}

				throw new RanOutOfResolveStepsException(error.ToString().TrimEnd());
			}

			ImmutableDeserializer.ImmutableDatabase = null;

			var result = new Result();

			db.ValidateRecycleBin(result);

			db.PrePostImport();
			db.PostImport();

			db.Validate(result);

			if (!result.IsSuccess)
				throw new ImmutableValidationException(result.GetErrors());

			return db;
		}

		private static string Describe(Unresolved unresolved)
		{
			var type = Nullable.GetUnderlyingType(unresolved.MemberType) ?? unresolved.MemberType;

			if (typeof(Immutable).IsAssignableFrom(type))
				return $"no {type.Name} named '{unresolved.Value}'";

			return $"'{unresolved.Value}' could not be read as {FriendlyName(type)}";
		}

		private static string FriendlyName(Type type)
		{
			if (!type.IsGenericType)
				return type.Name;

			var name = type.Name.Substring(0, type.Name.IndexOf('`'));
			return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyName))}>";
		}

		private static void CheckNameConflicts(List<UnresolvedModel> unresolvedModels)
		{
			var seen = new Dictionary<string, UnresolvedModel>();

			foreach (var model in unresolvedModels)
			{
				var key = $"{model.Immutable.IsRetired}/{ImmutableDatabase.GetIdentifier(model.Immutable.GetType(), model.Row.GetCell(nameof(Immutable.Name))?.Trim())}";

				if (seen.TryGetValue(key, out var other))
					throw new ImmutableImportException($"Name conflict: {model.Row.Class} '{model.Row.Name}' is defined at {other.Row.Location} and {model.Row.Location}.");

				seen.Add(key, model);
			}
		}

		private static bool IsResolved(UnresolvedModel toResolve)
		{
			return toResolve.Unresolved.Count == 0;
		}

		/// <summary>Tries every pending member once. Returns true if any member resolved.</summary>
		private static bool ResolveUnresolvedModel(UnresolvedModel toResolve, ImmutableDatabase db)
		{
			var progress = false;
			var properties = toResolve.Unresolved.ToList();
			foreach (var property in properties)
			{
				if (Resolve(toResolve, property))
				{
					toResolve.Unresolved.Remove(property);
					progress = true;
				}
			}

			if (!toResolve.Added)
			{
				db.Add(toResolve.Immutable);
				toResolve.Added = true;
				progress = true;
			}

			return progress;
		}

		private static bool Resolve(UnresolvedModel model, Unresolved unresolved)
		{
			try
			{
				if (unresolved is UnresolvedProperty unresolvedProperty)
				{
					var propertyInfo = unresolvedProperty.PropertyInfo;

					if (TryDeserialize(propertyInfo, unresolved.Value, out var value))
					{
						if (propertyInfo.SetMethod == null)
						{
							propertyInfo.GetBackingField().SetValue(unresolved.Immutable, value);
						}
						else
						{
							propertyInfo.SetValue(unresolved.Immutable, value);
						}

						return true;
					}
				}

				if (unresolved is UnresolvedField unresolvedField)
				{
					var fieldInfo = unresolvedField.FieldInfo;

					if (TryDeserialize(fieldInfo, unresolved.Value, out var value))
					{
						fieldInfo.SetValue(model.Immutable, value);
						return true;
					}
				}

				return false;
			}
			catch (Exception e) when (e is not ImmutableImportException)
			{
				var reason = e.InnerException != null ? $"{e.Message} ({e.InnerException.Message})" : e.Message;
				throw new ImmutableImportException($"{model.Row.Location}: {model.Immutable.GetType().Name} '{model.Row.Name}', column {unresolved.Header} = '{unresolved.Value}': {reason}", e);
			}
		}

		internal static bool TryDeserialize(PropertyInfo propertyInfo, string data, out object value)
		{
			var type = propertyInfo.PropertyType;
			return TryDeserialize(type, data, out value);
		}

		internal static bool TryDeserialize(FieldInfo fieldInfo, string data, out object value)
		{
			var type = fieldInfo.FieldType;
			return TryDeserialize(type, data, out value);
		}

		internal static bool TryDeserialize(Type type, string data, out object value)
		{
			data = data.Trim();

			if (IsList(type))
			{
				if (TryDeserializeList(type, data, out value))
				{
					return true;
				}

				return false;
			}

			if (string.IsNullOrEmpty(data))
			{
				value = null;
				return true;
			}

			if (type == typeof(string))
			{
				value = data;
				return true;
			}

			var deserializer = Deserializers.FirstOrDefault(x => x.CanDeserialize(type));

			if (deserializer == null)
			{
				throw new Exception($"No deserializer found for type {type.Name}");
			}

			if (deserializer.Deserialize(type, data, out value))
			{
				AddBreadcrumb($"Deserialized with {deserializer.GetType().Name}");
				return true;
			}

			return false;
		}

		private static Type FindImmutableType(List<Type> types, ImmutableRow row)
		{
			var className = row.Class.Trim();
			var type = types.FirstOrDefault(x => x.Name == className);

			if (type == null)
				throw new ImmutableImportException($"{row.Location}: class '{className}' not found.");

			if (!typeof(Immutable).IsAssignableFrom(type))
				throw new ImmutableImportException($"{row.Location}: {type} is not {nameof(Immutable)}.");

			return type;
		}

		private static UnresolvedModel ParseRow(List<Type> types, ImmutableRow row)
		{
			// read class out first so we know what class to create
			var type = FindImmutableType(types, row);

			var immutable = (Immutable)Activator.CreateInstance(type);

			if (row.Retired)
			{
				immutable.IsRetired = true;
				immutable.RemovedIn = row.RemovedIn;
			}

			var unresolvedModel = new UnresolvedModel
			{
				Immutable = immutable,
				Unresolved = new List<Unresolved>(),
				Row = row
			};

			// Build a cache for aggregate groups (so multiple headers like Xyz(Foo), Xyz(Bar) combine into one)
			var compoundGroups = new Dictionary<string, List<(string subKey, string value)>>();

			// read model data based on headers
			foreach (var cell in row.Cells.Where(x => x.Key != ClassHeader))
			{
				var header = cell.Key;

				if (string.IsNullOrEmpty(header))
				{
					throw new ImmutableImportException($"{row.Location}: {type.Name} has an empty header. This is not supported.");
				}

				if (IsCompoundHeader(header))
				{
					var openParen = header.IndexOf('(');
					var closeParen = header.LastIndexOf(')');

					var baseName = header.Substring(0, openParen);
					var innerName = header.Substring(openParen + 1, closeParen - openParen - 1);

					if (string.IsNullOrEmpty(cell.Value))
						continue;

					if (!compoundGroups.TryGetValue(baseName, out var list))
					{
						list = new List<(string subKey, string value)>();
						compoundGroups[baseName] = list;
					}

					list.Add((innerName, cell.Value));
					continue;
				}

				if (!TryAddMember(unresolvedModel, type, header, header, cell.Value))
					throw new ImmutableImportException($"{row.Location}: {type.Name} has no field or property '{header}'.");
			}

			foreach (var kvp in compoundGroups)
			{
				var baseName = kvp.Key;

				var joined = string.Join(ArrayDelimiter, kvp.Value.Select(x => $"{x.subKey}{InlineDelimiter}{x.value}"));

				if (!TryAddMember(unresolvedModel, type, baseName, $"{baseName}(...)", joined))
					throw new ImmutableImportException($"{row.Location}: {type.Name} has no aggregate base field '{baseName}'.");
			}

			return unresolvedModel;
		}

		private static bool TryAddMember(UnresolvedModel model, Type type, string memberName, string header, string value)
		{
			var propertyInfo = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (propertyInfo != null)
			{
				model.Unresolved.Add(new UnresolvedProperty
				{
					Immutable = model.Immutable,
					PropertyInfo = propertyInfo,
					Header = header,
					Value = value
				});
				return true;
			}

			var fieldInfo = type.GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (fieldInfo != null)
			{
				model.Unresolved.Add(new UnresolvedField
				{
					Immutable = model.Immutable,
					FieldInfo = fieldInfo,
					Header = header,
					Value = value
				});
				return true;
			}

			return false;
		}

		private static bool IsCompoundHeader(string header)
		{
			return header.Contains("(") && header.EndsWith(")");
		}

		public static List<Type> GetTypes()
		{
			var types = new List<Type>();
			types.Add(typeof(Immutable));
			if (AssemblyProvider != null)
			{
				foreach (var assembly in AssemblyProvider.GetAssemblies())
				{
					types.AddRange(assembly.GetTypes());
				}
			}
			types = types.Distinct().ToList();
			return types;
		}

		/// <summary>
		/// Gets class header index from given csv headers. Returns -1 if not found.
		/// </summary>
		/// <param name="headers"></param>
		/// <returns></returns>
		private static Dictionary<string, int> GetHeaderDictionary(string[] headers)
		{
			var dictionary = new Dictionary<string, int>();
			for (var i = 0; i < headers.Length; ++i)
			{
				dictionary.Add(headers[i], i);
			}

			return dictionary;
		}

		private static bool IsList(Type type)
		{
			if (type.IsArray)
				return true;

			if (typeof(WeightedList<>).IsAssignableFromRecursive(type))
				return true;

			if (typeof(ImmutableList<>).IsAssignableFromRecursive(type))
				return true;

			if (typeof(IReadOnlyList<>).IsAssignableFromRecursive(type))
				throw new Exception("Obsolete, use ImmutableList");

			if (typeof(List<>).IsAssignableFromRecursive(type))
				throw new Exception("Obsolete, use ImmutableList");

			return false;
		}

		private static bool TryDeserializeList(Type type, string data, out object deserializedObject)
		{
			if (type.IsNullable() && string.IsNullOrEmpty(data))
			{
				deserializedObject = null;
				return true;
			}

			if (type.IsArray)
			{
				var elementType = type.GetElementType();
				if (ToArray(elementType, data, out deserializedObject))
					return true;
			}

			if (typeof(WeightedList<>).IsAssignableFromRecursive(type))
			{
				var elementType = type.GetGenericArguments()[0];
				if (ToWeightedList(elementType, data, out deserializedObject))
					return true;
			}

			if (typeof(ImmutableList<>).IsAssignableFromRecursive(type))
			{
				var elementType = type.GetGenericArguments()[0];
				if (ToImmutableList(elementType, data, out deserializedObject))
					return true;
			}

			deserializedObject = null;
			return false;
		}

		private static bool ToArray(Type type, string data, out object deserializedArray)
		{
			if (ToList(type, data, out var list))
			{
				var toArray = list.GetType().GetMethod("ToArray");
				deserializedArray = toArray.Invoke(list, null);
				return true;
			}

			deserializedArray = null;
			return false;
		}

		private static bool ToImmutableList(Type type, string data, out object deserializedObject)
		{
			var genericType = typeof(ImmutableList<>).MakeGenericType(type);

			if (string.IsNullOrEmpty(data))
			{
				deserializedObject = genericType.GetField("Empty").GetValue(null);
				return true;
			}

			if (TryParseSequence(type, data, out var deserializedList))
			{
				deserializedObject = deserializedList.ToImmutableList();
				return true;
			}

			var list = genericType.GetField("Empty").GetValue(null);
			var add = genericType.GetMethod("Add");

			var arrays = ParseArrays(data).ToList();
			if (arrays.Count > 1)
			{
				throw new Exception($"Malformed data {data}");
			}

			var split = arrays[0].Split(ArrayDelimiter);
			for (var i = 0; i < split.Length; ++i)
			{
				try
				{
					if (TryDeserialize(type, split[i], out var value))
					{
						list = add.Invoke(list, new[] { value });
					}
					else
					{
						deserializedObject = null;
						return false;
					}
				}
				catch (Exception e)
				{
					throw new Exception($"Malformed split data {split[i]}", e);
				}
			}

			deserializedObject = list;
			return true;
		}

		private static IEnumerable<string> ParseArrays(string data)
		{
			var openBracketCount = 0;
			var startIndex = -1;

			if (!data.StartsWith("["))
			{
				yield return data;
				yield break;
			}

			for (var i = 0; i < data.Length; i++)
			{
				var c = data[i];

				if (c == '[')
				{
					if (openBracketCount == 0)
					{
						startIndex = i + 1;
					}

					openBracketCount++;
				}
				else if (c == ']')
				{
					if (openBracketCount > 0)
					{
						openBracketCount--;
						if (openBracketCount == 0)
						{
							// A leading bracket can group the first field of an ordered list item,
							// rather than wrap the list itself: [Nested:a|b]|remainingField.
							if (i + 1 < data.Length && data[i + 1] == InlineDelimiter)
							{
								yield return data;
								yield break;
							}

							yield return data.Substring(startIndex, i - startIndex);
						}
					}
					else
					{
						throw new Exception($"Malformed data {data}");
					}
				}
			}
		}

		private static bool ToList(Type type, string data, out object deserializedObject)
		{
			var genericType = typeof(List<>).MakeGenericType(type);

			if (string.IsNullOrEmpty(data))
			{
				deserializedObject = Activator.CreateInstance(genericType, 0);
				return true;
			}

			if (TryParseSequence(type, data, out var deserializedList))
			{
				deserializedObject = deserializedList;
				return true;
			}

			var split = data.Split(ArrayDelimiter);
			var list = Activator.CreateInstance(genericType, split.Length);
			var add = genericType.GetMethod("Add");

			for (var i = 0; i < split.Length; ++i)
			{
				if (TryDeserialize(type, split[i], out var value))
					add.Invoke(list, new[] { value });
				else
				{
					deserializedObject = null;
					return false;
				}
			}

			deserializedObject = list;
			return true;
		}

		private static bool TryParseSequence(Type type, string data, out List<int> deserializedList)
		{
			if (type == typeof(int) && StepParser.TryParseStepString(data, out var stepType, out var steps, out var min, out var max))
			{
				var list = new List<int>();

				var content = StepGenerator.GenerateSteps(stepType, steps, min, max);
				foreach (var i in content)
				{
					list.Add(i);
				}

				deserializedList = list;
				return true;
			}

			deserializedList = null;
			return false;
		}

		private static bool ToWeightedList(Type type, string data, out object deserializedObject)
		{
			if (string.IsNullOrEmpty(data))
			{
				throw new Exception($"Weighted list cannot be empty.");
			}

			var split = data.Split(ArrayDelimiter);
			var genericType = typeof(WeightedList<>).MakeGenericType(type);
			var weightedList = Activator.CreateInstance(genericType);

			var methods = genericType.GetMethods();
			var addMethod = methods.First(x => x.Name == "Add" && x.GetParameters().Length == 2);

			foreach (var item in split)
			{
				var chunks = item.Split(InlineDelimiter);

				if (chunks.Length != 2 || !double.TryParse(chunks[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
					throw new Exception($"Could not parse weighted item '{item}'; expected value{InlineDelimiter}weight.");

				if (TryDeserialize(type, chunks[0], out var value))
					addMethod.Invoke(weightedList, new object[] { value, weight });
				else
				{
					deserializedObject = null;
					return false;
				}
			}

			deserializedObject = weightedList;
			return true;
		}
	}
}
