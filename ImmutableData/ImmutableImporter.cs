using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ImmutableData.Deserializers;
using ImmutableData.Extensions;
using ImmutableData.Utilities;
using LiteCsvParser;
using Mono.Reflection;

namespace ImmutableData
{
	public class ImmutableImporterLog
	{
	}

	public static class ImmutableImporter
	{
		public static IDeserializerProvider DeserializerProvider = new BasicDeserializerProvider();
		public static IAssemblyProvider AssemblyProvider = new BasicAssemblyProvider();

		public const char ArrayStart = '[';
		public const char ArrayEnd = ']';
		public const char ArrayDelimiter = ',';
		public const char InlineDelimiter = ';';
		public const string ClassHeader = "Class";
		public const string KeyHeader = "Key";
		public const string ValueHeader = "Value";
		private const int MaxResolveIterations = 1000;

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
			public string SourceFile;
		}

		private abstract class Unresolved
		{
			public Immutable Immutable;
			public abstract string Name { get; }
			public string Value;
		}

		private class UnresolvedField : Unresolved
		{
			public override string Name => FieldInfo.Name;
			public FieldInfo FieldInfo;
		}

		private class UnresolvedProperty : Unresolved
		{
			public override string Name => PropertyInfo.Name;
			public PropertyInfo PropertyInfo;
		}

		public static readonly List<string> Breadcrumbs = new();

		public static ImmutableDatabase Import(string[] files)
		{
			files = files.Distinct().ToArray();
			try
			{
				return ResolveModels(GetUnresolvedModelsFromFiles(files.Distinct()).ToList());
			}
			catch (Exception e)
			{
				var error = new StringBuilder();
				error.AppendLine("Last 10 breadcrumbs:");
				var count = Math.Min(10, Breadcrumbs.Count);
				for (var i = Breadcrumbs.Count - count; i < Breadcrumbs.Count; ++i)
				{
					error.AppendLine(Breadcrumbs[i]);
				}

				throw new Exception(error.ToString(), e);
			}
		}

		private static ImmutableDatabase ResolveModels(List<UnresolvedModel> unresolvedModels)
		{
			var db = new ImmutableDatabase();

			CheckNameConflicts(unresolvedModels);

			ImmutableDeserializer.ImmutableDatabase = db;

			// start resolving models
			var unresolvedQueue = new Queue<UnresolvedModel>(unresolvedModels);

			var iterations = 0;
			while (unresolvedQueue.Count > 0)
			{
				// try to resolve model
				var toResolve = unresolvedQueue.Dequeue();

				ResolveUnresolvedModel(toResolve, db);

				if (!IsResolved(toResolve))
					unresolvedQueue.Enqueue(toResolve);

				iterations++;
				if (iterations > MaxResolveIterations)
				{
					var error = new StringBuilder();
					error.AppendLine("Ran out of resolve steps.");
					error.AppendLine($"Models left to resolve:");

					foreach (var unresolvedModel in unresolvedQueue)
					{
						if (unresolvedModel.Unresolved.Count == 0)
							continue;

						error.AppendLine($"{unresolvedModel.Immutable.Name}");
						foreach (var unresolved in unresolvedModel.Unresolved)
						{
							error.AppendLine($"\t{unresolved.Name}:{unresolved.Value}");
						}
					}

					// append last 10 breadcrumbs
					error.AppendLine("Last 10 breadcrumbs:");
					var count = Math.Min(10, Breadcrumbs.Count);
					for (var i = Breadcrumbs.Count - count; i < Breadcrumbs.Count; ++i)
					{
						error.AppendLine(Breadcrumbs[i]);
					}

					// left here for debugging
					ResolveUnresolvedModel(toResolve, db);

					throw new RanOutOfResolveStepsException(error.ToString());
				}
			}

			ImmutableDeserializer.ImmutableDatabase = null;

			var result = new Result();

			db.PrePostImport();
			db.PostImport();

			if (!db.Validate(result))
				throw new ImmutableValidationException(result.GetError());

			return db;
		}

		private static void CheckNameConflicts(List<UnresolvedModel> unresolvedModels)
		{
			foreach (var a in unresolvedModels)
			{
				var aName = a.Unresolved.FirstOrDefault(x => x.Name == "Name")?.Value ?? "";
				foreach (var b in unresolvedModels)
				{
					if (ReferenceEquals(a, b))
						continue;

					var bName = b.Unresolved.FirstOrDefault(x => x.Name == "Name")?.Value ?? "";
					if (a.Immutable.GetType() == b.Immutable.GetType() && aName == bName)
						throw new Exception($"Name conflict! {aName}=={bName} ({a.SourceFile}, {b.SourceFile}");
				}
			}
		}

		private static bool IsResolved(UnresolvedModel toResolve)
		{
			return toResolve.Unresolved.Count == 0;
		}

		private static void ResolveUnresolvedModel(UnresolvedModel toResolve, ImmutableDatabase db)
		{
			var properties = toResolve.Unresolved.ToList();
			foreach (var property in properties)
			{
				if (Resolve(toResolve.Immutable, property))
					toResolve.Unresolved.Remove(property);
			}

			if (!db.Exists(toResolve.Immutable.GetType(), toResolve.Immutable.Name))
				db.Add(toResolve.Immutable);
		}

		private static bool Resolve(Immutable immutable, Unresolved unresolved)
		{
			try
			{
				if (unresolved is UnresolvedProperty unresolvedProperty)
				{
					var propertyInfo = unresolvedProperty.PropertyInfo;

					if (TryDeserialize(propertyInfo, unresolved.Value, out var value))
					{
						// TODO: if init only ever becomes a thing in unity, this can be removed
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
						fieldInfo.SetValue(immutable, value);
						return true;
					}
				}

				return false;
			}
			catch (Exception e)
			{
#if UNITY_EDITOR
				UnityEngine.Debug.LogError($"{e.Message}\n{immutable.Name}/{immutable.GetType()} : {unresolved.Name}={unresolved.Value}");
#endif
				throw;
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

			var deserializer = DeserializerProvider.Deserializers.FirstOrDefault(x => x.CanDeserialize(type));

			if (deserializer == null)
			{
				throw new Exception($"No deserializer found for type {type.Name}");
			}

			if (deserializer.Deserialize(type, data, out value))
			{
				return true;
			}

			return false;
		}

		private static IEnumerable<UnresolvedModel> GetUnresolvedModelsFromFiles(IEnumerable<string> files)
		{
			// get all unresolved models from files
			foreach (var file in files)
			{
				Breadcrumbs.Add($"Importing file {file}");
				foreach (var unresolved in GetUnresolvedModelsFromFile(file))
				{
					yield return unresolved;
				}
			}
		}

		private static IEnumerable<UnresolvedModel> GetUnresolvedModelsFromFile(string path)
		{
			using var reader = new CsvFileReader(path, EmptyLineBehavior.Ignore);

			var row = new List<string>();
			Dictionary<string, int> headers = null;

			// read headers first
			if (!reader.ReadRow(row))
				throw new Exception("No data in csv.");

			headers = new Dictionary<string, int>(row.Count);
			for (var i = 0; i < row.Count; ++i)
			{
				headers.Add(row[i], i);
			}

			var types = GetTypes();

			var isKeyValuePairs = headers.Count == 2 && headers.ContainsKey(KeyHeader) && headers.ContainsKey(ValueHeader);

			if (isKeyValuePairs)
			{
				// since csv data is in kvpairs (column 0 = key, column 1 = value) we need a special way
				// to read data out where we read both keys and values and put them in headers and one row
				headers.Clear();
				var values = new List<string>();
				var n = 0;
				while (reader.ReadRow(row))
				{
					headers.Add(row[0], n);
					values.Add(row[1]);
					n++;
				}

				row.Clear();
				row.AddRange(values);

				if (!headers.ContainsKey(ClassHeader))
				{
					throw new InvalidDataException($"CSV {path} does not contain class header.");
				}

				yield return ParseRow(path, types, headers, row);
				yield break; // do not parse file normally
			}

			if (!headers.ContainsKey(ClassHeader))
			{
				throw new InvalidDataException($"CSV {path} does not contain class header.");
			}

			while (reader.ReadRow(row))
			{
				yield return ParseRow(path, types, headers, row);
			}
		}

		private static UnresolvedModel ParseRow(string path, List<Type> types, Dictionary<string, int> headers, List<string> row)
		{
			// read class out first so we know what class to create
			var type = types.FirstOrDefault(x => x.Name == row[headers[ClassHeader]]);

			if (type == null)
			{
				throw new InvalidDataException($"{path} {row[headers[ClassHeader]]} not found in assembly.");
			}

			if (!typeof(Immutable).IsAssignableFrom(type))
			{
				throw new InvalidDataException($"{path} {type} is not {nameof(Immutable)}.");
			}

			var immutable = (Immutable)Activator.CreateInstance(type);

			var unresolvedModel = new UnresolvedModel
			{
				Immutable = immutable,
				Unresolved = new List<Unresolved>(),
				SourceFile = path
			};

			// Build a cache for aggregate groups (so multiple headers like Xyz(Foo), Xyz(Bar) combine into one)
			var compoundGroups = new Dictionary<string, List<(string subKey, string value)>>();

			// read model data based on headers
			foreach (var header in headers.Where(x => x.Key != ClassHeader))
			{
				if (string.IsNullOrEmpty(header.Key))
				{
					throw new InvalidDataException($"{path} {type} has an empty header. This is not supported.");
				}

				if (IsCompoundHeader(header))
				{
					var key = header.Key;
					var openParen = key.IndexOf('(');
					var closeParen = key.LastIndexOf(')');

					var baseName = key.Substring(0, openParen);
					var innerName = key.Substring(openParen + 1, closeParen - openParen - 1);

					var value = row[header.Value];
					if (string.IsNullOrEmpty(value))
						continue;

					if (!compoundGroups.TryGetValue(baseName, out var list))
					{
						list = new List<(string subKey, string value)>();
						compoundGroups[baseName] = list;
					}

					list.Add((innerName, value));
					continue;
				}

				var propertyInfo = type.GetProperty(header.Key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (propertyInfo != null)
				{
					unresolvedModel.Unresolved.Add(new UnresolvedProperty
					{
						Immutable = immutable,
						PropertyInfo = propertyInfo,
						Value = row[header.Value]
					});
					continue;
				}

				var fieldInfo = type.GetField(header.Key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (fieldInfo != null)
				{
					unresolvedModel.Unresolved.Add(new UnresolvedField
					{
						Immutable = immutable,
						FieldInfo = fieldInfo,
						Value = row[header.Value]
					});
					continue;
				}

				throw new InvalidDataException($"{path} {type} does not contain field {header.Key}");
			}

			foreach (var kvp in compoundGroups)
			{
				var baseName = kvp.Key;
				var joined = string.Join(",", kvp.Value.Select(x => $"{x.subKey};{x.value}"));

				var fieldInfo = type.GetField(baseName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (fieldInfo != null)
				{
					unresolvedModel.Unresolved.Add(new UnresolvedField
					{
						Immutable = immutable,
						FieldInfo = fieldInfo,
						Value = joined
					});
					continue;
				}

				var propertyInfo = type.GetProperty(baseName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (propertyInfo != null)
				{
					unresolvedModel.Unresolved.Add(new UnresolvedProperty
					{
						Immutable = immutable,
						PropertyInfo = propertyInfo,
						Value = joined
					});
					continue;
				}

				throw new InvalidDataException($"{path} {type} does not contain aggregate base field {baseName}");
			}

			return unresolvedModel;
		}

		private static bool IsCompoundHeader(KeyValuePair<string, int> header)
		{
			return header.Key.Contains("(") && header.Key.EndsWith(")");
		}

		public static List<Type> GetTypes()
		{
			var types = new List<Type>();
			foreach (var assembly in AssemblyProvider.Assemblies)
			{
				types.AddRange(assembly.GetTypes());
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
				var chunks = item.Split(':');

				if (!double.TryParse(chunks[1], out var weight))
					throw new Exception($"Could not parse weight value ({chunks[1]}).");

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