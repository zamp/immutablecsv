#nullable enable

using System.Collections.Generic;

namespace ImmutableData.Csv
{
	/// <summary>
	/// One immutable as read from CSV, before import: its cells by header plus where it came from. Hosts can build,
	/// merge or filter rows (for example to layer mods) and hand the result to <see cref="ImmutableImporter.Import(IEnumerable{ImmutableRow})"/>.
	/// </summary>
	public sealed class ImmutableRow
	{
		public const string ReplacedByHeader = "ReplacedBy";
		public const string RemovedInHeader = "RemovedIn";

		public ImmutableRow(string source, int line, IReadOnlyList<KeyValuePair<string, string>> cells, bool retired = false, string? replacedBy = null, string? removedIn = null, IReadOnlyDictionary<string, string>? cellLocations = null)
		{
			Source = source;
			Line = line;
			Cells = cells;
			CellLocations = cellLocations;
			Retired = retired;
			ReplacedBy = string.IsNullOrWhiteSpace(replacedBy) ? null : replacedBy!.Trim();
			RemovedIn = string.IsNullOrWhiteSpace(removedIn) ? null : removedIn!.Trim();
		}

		/// <summary>The file (or other source) the row came from.</summary>
		public string Source { get; }

		/// <summary>The 1-based line the row starts on in <see cref="Source"/>.</summary>
		public int Line { get; }

		/// <summary>Header and cell value pairs, in column order, excluding recycle-bin metadata.</summary>
		public IReadOnlyList<KeyValuePair<string, string>> Cells { get; }

		/// <summary>True for a row from a recycle bin: it loads as a retired immutable, or as an alias when <see cref="ReplacedBy"/> is set.</summary>
		public bool Retired { get; }

		/// <summary>For a recycled row, the name of the row that takes over from it (a rename or merge).</summary>
		public string? ReplacedBy { get; }

		/// <summary>For a recycled row, the version the row was retired in. Informational.</summary>
		public string? RemovedIn { get; }

		/// <summary>
		/// For a row merged from patches: where each cell came from, as <c>file:line</c>. Cells not listed come from
		/// <see cref="Location"/>.
		/// </summary>
		public IReadOnlyDictionary<string, string>? CellLocations { get; }

		public string Class => GetCell(ImmutableImporter.ClassHeader) ?? "";

		public string Name => GetCell(nameof(Immutable.Name)) ?? "";

		/// <summary>Where the row came from, as <c>file:line</c>.</summary>
		public string Location => $"{Source}:{Line}";

		/// <summary>Where a cell came from: its patch row, or this row.</summary>
		public string LocationOf(string header)
		{
			return CellLocations != null && CellLocations.TryGetValue(header, out var location) ? location : Location;
		}

		public string? GetCell(string header)
		{
			foreach (var cell in Cells)
			{
				if (cell.Key == header)
					return cell.Value;
			}

			return null;
		}

		public ImmutableRow WithCells(IReadOnlyList<KeyValuePair<string, string>> cells)
		{
			return new ImmutableRow(Source, Line, cells, Retired, ReplacedBy, RemovedIn, CellLocations);
		}
	}
}
