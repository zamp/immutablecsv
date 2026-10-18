#nullable enable

using System.Collections.Generic;
using System.IO;

namespace ImmutableData.Csv
{
	/// <summary>
	/// Turns a CSV file into <see cref="ImmutableRow"/>s. Handles both layouts: a row per immutable with a header line,
	/// and the two-column <c>Key,Value</c> layout that describes a single immutable.
	/// </summary>
	public static class CsvRowReader
	{
		public static List<ImmutableRow> ReadFile(string path, bool recycleBin = false)
		{
			using var reader = CsvReader.FromFile(path);
			return Read(reader, path, recycleBin);
		}

		public static List<ImmutableRow> ReadText(string text, string source, bool recycleBin = false)
		{
			using var reader = new CsvReader(new StringReader(text));
			return Read(reader, source, recycleBin);
		}

		public static List<ImmutableRow> Read(CsvReader reader, string source, bool recycleBin)
		{
			var rows = new List<ImmutableRow>();
			var row = new List<string>();

			if (!reader.ReadRow(row))
				throw new InvalidDataException($"{source}: no data in CSV.");

			var headers = new List<string>();
			foreach (var header in row)
				headers.Add(header.Trim());

			var seen = new HashSet<string>();
			for (var i = 0; i < headers.Count; ++i)
			{
				if (string.IsNullOrEmpty(headers[i]))
					throw new InvalidDataException($"{source}:{reader.RowLine}: column {i + 1} has an empty header.");

				if (!seen.Add(headers[i]))
					throw new InvalidDataException($"{source}:{reader.RowLine}: header '{headers[i]}' appears twice.");
			}

			var isKeyValuePairs = headers.Count == 2 && headers[0] == ImmutableImporter.KeyHeader && headers[1] == ImmutableImporter.ValueHeader;

			if (isKeyValuePairs)
			{
				var cells = new List<KeyValuePair<string, string>>();
				var firstLine = 0;
				var keys = new HashSet<string>();

				while (reader.ReadRow(row))
				{
					if (firstLine == 0)
						firstLine = reader.RowLine;

					var key = row[0].Trim();
					var value = row.Count > 1 ? row[1] : "";

					if (string.IsNullOrEmpty(key))
						throw new InvalidDataException($"{source}:{reader.RowLine}: empty key.");

					if (row.Count > 2)
						throw new InvalidDataException($"{source}:{reader.RowLine}: a Key,Value row has {row.Count} cells; quote the value if it contains commas.");

					if (!keys.Add(key))
						throw new InvalidDataException($"{source}:{reader.RowLine}: key '{key}' appears twice.");

					cells.Add(new KeyValuePair<string, string>(key, value));
				}

				if (!keys.Contains(ImmutableImporter.ClassHeader))
					throw new InvalidDataException($"{source}: CSV does not contain a {ImmutableImporter.ClassHeader} key.");

				rows.Add(CreateRow(source, firstLine, cells, recycleBin));
				return rows;
			}

			if (!headers.Contains(ImmutableImporter.ClassHeader))
				throw new InvalidDataException($"{source}: CSV does not contain a {ImmutableImporter.ClassHeader} header.");

			while (reader.ReadRow(row))
			{
				if (row.Count > headers.Count)
					throw new InvalidDataException($"{source}:{reader.RowLine}: row has {row.Count} cells but there are only {headers.Count} headers.");

				var cells = new List<KeyValuePair<string, string>>(headers.Count);
				for (var i = 0; i < headers.Count; ++i)
					cells.Add(new KeyValuePair<string, string>(headers[i], i < row.Count ? row[i] : ""));

				rows.Add(CreateRow(source, reader.RowLine, cells, recycleBin));
			}

			return rows;
		}

		private static ImmutableRow CreateRow(string source, int line, List<KeyValuePair<string, string>> cells, bool recycleBin)
		{
			if (!recycleBin)
				return new ImmutableRow(source, line, cells);

			string? replacedBy = null;
			string? removedIn = null;
			var dataCells = new List<KeyValuePair<string, string>>(cells.Count);

			foreach (var cell in cells)
			{
				if (cell.Key == ImmutableRow.ReplacedByHeader)
					replacedBy = cell.Value;
				else if (cell.Key == ImmutableRow.RemovedInHeader)
					removedIn = cell.Value;
				else
					dataCells.Add(cell);
			}

			return new ImmutableRow(source, line, dataCells, true, replacedBy, removedIn);
		}
	}
}
