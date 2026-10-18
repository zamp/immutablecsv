#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Csv
{
	/// <summary>
	/// Layers rows from several sources, such as mods in load order. A later row naming the same immutable (same
	/// name root type and <c>Name</c>) is a patch: it changes only the columns it has, and every other field keeps
	/// the earlier value. So a mod can ship a small file with just <c>Class,Name,Speed</c>. A row with <c>Remove</c>
	/// set to <c>true</c> deletes the earlier row. Other rows add new immutables. The <c>Remove</c> column is stripped.
	/// A patch or remove row naming a row the recycle bin renamed (<c>ReplacedBy</c>) applies to the new name.
	/// </summary>
	public static class ImmutableRowLayers
	{
		public const string RemoveHeader = "Remove";

		private const int MAX_RENAMES = 64;

		/// <summary>Merges the layers; <paramref name="recycled"/> are the recycle-bin rows, whose renames patches follow.</summary>
		public static List<ImmutableRow> Merge(IEnumerable<IEnumerable<ImmutableRow>> layers, List<string>? warnings = null, IEnumerable<ImmutableRow>? recycled = null)
		{
			var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
			foreach (var type in ImmutableImporter.GetTypes())
				typesByName.TryAdd(type.Name, type);

			var renames = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var row in recycled ?? Enumerable.Empty<ImmutableRow>())
			{
				if (row.Retired && row.ReplacedBy != null)
					renames.TryAdd(GetKey(row.Class, row.Name, false, typesByName), row.ReplacedBy);
			}

			var merged = new List<ImmutableRow?>();
			var index = new Dictionary<string, int>(StringComparer.Ordinal);

			foreach (var layer in layers)
			{
				// Rows within one layer never patch each other; a duplicate there is a name conflict for the importer.
				var seenInLayer = new HashSet<string>(StringComparer.Ordinal);

				foreach (var row in layer)
				{
					var remove = IsRemove(row);
					var stripped = row.WithCells(row.Cells.Where(x => x.Key != RemoveHeader).ToList());
					var key = GetKey(row.Class, row.Name, row.Retired, typesByName);

					if (!row.Retired && !index.ContainsKey(key) && FindRenamed(row, renames, index, typesByName) is { } renamed)
					{
						warnings?.Add($"{row.Location}: {row.Class} '{row.Name}' was renamed to '{renamed}' in the recycle bin; this row changes '{renamed}'.");
						stripped = stripped.WithCells(stripped.Cells.Select(x => x.Key == nameof(Immutable.Name) ? new KeyValuePair<string, string>(x.Key, renamed) : x).ToList());
						key = GetKey(row.Class, renamed, false, typesByName);
					}

					var firstInLayer = seenInLayer.Add(key);

					if (firstInLayer && index.TryGetValue(key, out var position))
					{
						if (remove)
						{
							merged[position] = null;
							index.Remove(key);
						}
						else
						{
							merged[position] = Patch(merged[position]!, stripped);
						}

						continue;
					}

					if (remove)
					{
						warnings?.Add($"{row.Location}: removes {row.Class} '{row.Name}', which nothing earlier defines.");
						continue;
					}

					if (firstInLayer)
						index[key] = merged.Count;

					merged.Add(stripped);
				}
			}

			return merged.Where(x => x != null).Select(x => x!).ToList();
		}

		/// <summary>The earlier row with the patch's columns written over it, remembering where each cell came from.</summary>
		public static ImmutableRow Patch(ImmutableRow original, ImmutableRow patch)
		{
			var cells = original.Cells.ToList();
			var locations = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var cell in original.Cells)
				locations[cell.Key] = original.LocationOf(cell.Key);

			foreach (var cell in patch.Cells)
			{
				// Empty cells are "not set here", so a wide patch file only changes the cells it fills in.
				if (string.IsNullOrWhiteSpace(cell.Value))
					continue;

				var existing = cells.FindIndex(x => x.Key == cell.Key);

				if (existing >= 0)
					cells[existing] = cell;
				else
					cells.Add(cell);

				locations[cell.Key] = patch.Location;
			}

			return new ImmutableRow(original.Source, original.Line, cells, original.Retired || patch.Retired, patch.ReplacedBy ?? original.ReplacedBy, patch.RemovedIn ?? original.RemovedIn, locations);
		}

		private static bool IsRemove(ImmutableRow row)
		{
			var cell = row.GetCell(RemoveHeader)?.Trim();

			if (string.IsNullOrEmpty(cell) || cell.Equals("false", StringComparison.OrdinalIgnoreCase))
				return false;

			if (cell.Equals("true", StringComparison.OrdinalIgnoreCase))
				return true;

			throw new FormatException($"{row.Location}: {RemoveHeader} must be true or false, not '{cell}'.");
		}

		/// <summary>The name an earlier row has now, following the recycle bin's renames of the row's name; null when none is earlier.</summary>
		private static string? FindRenamed(ImmutableRow row, Dictionary<string, string> renames, Dictionary<string, int> index, Dictionary<string, Type> typesByName)
		{
			var name = row.Name;

			for (var i = 0; i < MAX_RENAMES && renames.TryGetValue(GetKey(row.Class, name, false, typesByName), out var next); ++i)
			{
				name = next;

				if (index.ContainsKey(GetKey(row.Class, name, false, typesByName)))
					return name.Trim();
			}

			return null;
		}

		/// <summary>Rows name the same immutable when their name root type (or class, if unknown) and Name match.</summary>
		private static string GetKey(string className, string name, bool retired, Dictionary<string, Type> typesByName)
		{
			className = className.Trim();
			var root = typesByName.TryGetValue(className, out var type) ? ImmutableDatabase.GetRootBaseType(type).Name : className;
			return $"{(retired ? "recycled" : "active")}/{root}/{name.Trim()}";
		}
	}
}
