#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ImmutableData
{
	public class ImmutableDatabase
	{
		private const int MaxAliasDepth = 64;

		private readonly Dictionary<Key, Immutable> _database = new();
		private readonly Dictionary<Key, Immutable> _retired = new();
		private readonly Dictionary<Key, string> _aliases = new();
		private readonly List<string> _warnings = new();

		/// <summary>Non-fatal import notes, such as retired references that were dropped.</summary>
		public IReadOnlyList<string> Warnings => _warnings;

		/// <summary>Number of active (non-retired) immutables.</summary>
		public int Count => _database.Count;

		internal static string GetIdentifier(Type type, string? name)
		{
			return GetKey(type, name).ToString();
		}

		// a name under its root type; a struct, so looking a name up builds no string
		private static Key GetKey(Type type, string? name)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			return new Key(GetRootBaseType(type), name ?? string.Empty);
		}

		private readonly struct Key : IEquatable<Key>
		{
			public readonly Type Root;
			public readonly string Name;

			public Key(Type root, string name)
			{
				Root = root;
				Name = name;
			}

			public bool Equals(Key other)
			{
				return Root == other.Root && string.Equals(Name, other.Name, StringComparison.Ordinal);
			}

			public override bool Equals(object? obj)
			{
				return obj is Key other && Equals(other);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(Root, Name);
			}

			public override string ToString()
			{
				return $"{Root.Name}/{Name}";
			}
		}

		/// <summary>The type whose name namespaces <paramref name="type"/>'s names: the class just below <see cref="Immutable"/>, or just below the nearest ancestor marked <see cref="NotImmutableRoot"/>.</summary>
		public static Type GetRootBaseType(Type type)
		{
			var current = type;

			while (current.BaseType != null && current.BaseType != typeof(object) && current.BaseType != typeof(Immutable) && !current.BaseType.IsDefined(typeof(NotImmutableRoot), false))
				current = current.BaseType;

			return current;
		}

		/// <summary>
		/// Add a single model to the database
		/// </summary>
		internal void Add<T>(T immutable) where T : Immutable
		{
			var key = GetKey(immutable.GetType(), immutable.Name);
			var target = immutable.IsRetired ? _retired : _database;

			if (!target.TryAdd(key, immutable))
				throw new Exception($"Immutable with name {immutable.Name} already exists.");
		}

		internal void AddAlias(Type type, string oldName, string newName)
		{
			var key = GetKey(type, oldName);

			if (!_aliases.TryAdd(key, newName))
				throw new Exception($"{type.Name} {oldName} is replaced twice in the recycle bin.");
		}

		internal void AddWarning(string warning)
		{
			// a cell read again on a later pass warns once
			if (!_warnings.Contains(warning))
				_warnings.Add(warning);
		}

		/// <summary>
		/// Gets immutable based on the name. Follows recycle-bin renames and finds retired immutables.
		/// </summary>
		public T Get<T>(string name = "") where T : Immutable
		{
			return (T)Get(typeof(T), name);
		}

		/// <summary>
		/// Gets immutable based on name. Follows recycle-bin renames and finds retired immutables.
		/// </summary>
		public Immutable Get(Type type, string name)
		{
			if (TryGet(type, name, out var immutable))
				return immutable!;

			throw new Exception($"No immutable with name {name} of type {type.Name} exists.");
		}

		public bool TryGet<T>(string name, out T? data) where T : Immutable
		{
			if (TryGet(typeof(T), name, out var immutable) && immutable is T typed)
			{
				data = typed;
				return true;
			}

			data = null;
			return false;
		}

		public bool TryGet(Type type, string name, out Immutable? immutable)
		{
			var current = name;

			for (var depth = 0; depth < MaxAliasDepth; ++depth)
			{
				var key = GetKey(type, current);

				if (_database.TryGetValue(key, out immutable) && type.IsInstanceOfType(immutable))
					return true;

				if (_retired.TryGetValue(key, out immutable) && type.IsInstanceOfType(immutable))
					return true;

				if (!_aliases.TryGetValue(key, out var replacement))
					break;

				current = replacement;
			}

			immutable = null;
			return false;
		}

		/// <summary>
		/// Gets an active immutable by name for new use, such as a name a script asks for: follows recycle-bin renames
		/// but never returns a retired immutable, which is kept only for old saves.
		/// </summary>
		public bool TryGetActive<T>(string name, [NotNullWhen(true)] out T? data) where T : Immutable
		{
			if (TryGet(name, out data) && !data!.IsRetired)
				return true;

			data = null;
			return false;
		}

		/// <summary>
		/// Checks if immutable exists, including retired ones and recycle-bin renames.
		/// </summary>
		public bool Exists<T>(string name) where T : Immutable
		{
			return Exists(typeof(T), name);
		}

		/// <summary>
		/// Checks if immutable exists, including retired ones and recycle-bin renames.
		/// </summary>
		public bool Exists(Type type, string name)
		{
			return TryGet(type, name, out _);
		}

		/// <summary>True when the name is an active immutable (not retired, not a rename).</summary>
		public bool IsActive(Type type, string name)
		{
			return _database.TryGetValue(GetKey(type, name), out var immutable) && type.IsInstanceOfType(immutable);
		}

		/// <summary>
		/// Gets active immutables of type. Retired immutables are never included.
		/// </summary>
		public IEnumerable<T> GetAll<T>() where T : Immutable
		{
			return _database.Values.OfType<T>();
		}

		/// <summary>Retired immutables of type, from recycle bins.</summary>
		public IEnumerable<T> GetRetired<T>() where T : Immutable
		{
			return _retired.Values.OfType<T>();
		}

		/// <summary>Validates every active immutable and the [Ordered] values in it, and collects all errors, each prefixed with the immutable.</summary>
		public bool Validate(Result result)
		{
			foreach (var immutable in _database.Values)
			{
				var before = result.Errors.Count;
				immutable.Validate(result);
				OrderedValues.Validate(immutable, result);
				result.PrefixErrors(before, $"{immutable}: ");
			}

			return result.IsSuccess;
		}

		/// <summary>Checks the recycle bin: no name both active and retired, renames point somewhere and do not loop.</summary>
		internal void ValidateRecycleBin(Result result)
		{
			foreach (var key in _retired.Keys)
			{
				if (_database.ContainsKey(key))
					result.AddError($"{_retired[key]} is both active and in the recycle bin.");
			}

			foreach (var alias in _aliases)
			{
				if (_database.ContainsKey(alias.Key) || _retired.ContainsKey(alias.Key))
					result.AddError($"{alias.Key} is replaced by {alias.Value} in the recycle bin but still exists.");

				var visited = new HashSet<Key> { alias.Key };
				var current = alias.Value;

				while (true)
				{
					var key = new Key(alias.Key.Root, current);

					if (_database.ContainsKey(key) || _retired.ContainsKey(key))
						break;

					if (!_aliases.TryGetValue(key, out var next))
					{
						result.AddError($"{alias.Key} is replaced by {alias.Value}, which does not exist.");
						break;
					}

					if (!visited.Add(key))
					{
						result.AddError($"{alias.Key} is part of a ReplacedBy loop.");
						break;
					}

					current = next;
				}
			}
		}

		public void PrePostImport()
		{
			foreach (var immutable in AllLoaded())
				immutable.PrePostImport(this);
		}

		public void PostImport()
		{
			foreach (var immutable in AllLoaded())
				immutable.PostImport(this);
		}

		public void PostImport<T>(T gameState)
		{
			foreach (var immutable in AllLoaded())
				immutable.PostImport(this, gameState);
		}

		private IEnumerable<Immutable> AllLoaded()
		{
			return _database.Values.Concat(_retired.Values).ToList();
		}
	}
}
