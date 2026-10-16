#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImmutableData.Csv
{
	/// <summary>
	/// Minimal RFC 4180 CSV reader: comma separated, double-quoted fields with "" as an escaped quote, quoted fields may
	/// span lines. Empty lines are skipped. Reports the line each row starts on, for error messages.
	/// </summary>
	public sealed class CsvReader : IDisposable
	{
		private readonly TextReader _reader;
		private int _line = 1;

		public CsvReader(TextReader reader)
		{
			_reader = reader;
		}

		public static CsvReader FromFile(string path)
		{
			return new CsvReader(new StreamReader(path, Encoding.UTF8, true));
		}

		/// <summary>The 1-based line number the last row returned by <see cref="ReadRow"/> started on.</summary>
		public int RowLine { get; private set; }

		/// <summary>Reads the next non-empty row into <paramref name="columns"/>. Returns false at the end of the input.</summary>
		public bool ReadRow(List<string> columns)
		{
			while (true)
			{
				columns.Clear();

				if (_reader.Peek() < 0)
					return false;

				RowLine = _line;

				if (!ReadRecord(columns))
					return false;

				if (columns.Count == 1 && columns[0].Length == 0)
					continue;

				return true;
			}
		}

		private bool ReadRecord(List<string> columns)
		{
			var field = new StringBuilder();
			var inQuotes = false;
			var fieldWasQuoted = false;
			var readAny = false;

			while (true)
			{
				var next = _reader.Read();

				if (next < 0)
				{
					if (inQuotes)
						throw new InvalidDataException($"Unterminated quoted field starting on line {RowLine}.");

					if (!readAny)
						return false;

					columns.Add(field.ToString());
					return true;
				}

				readAny = true;
				var c = (char)next;

				if (inQuotes)
				{
					if (c == '"')
					{
						if (_reader.Peek() == '"')
						{
							_reader.Read();
							field.Append('"');
						}
						else
						{
							inQuotes = false;
						}

						continue;
					}

					if (c == '\n')
						_line++;

					field.Append(c);
					continue;
				}

				switch (c)
				{
					case '"':
						if (field.Length == 0 && !fieldWasQuoted)
						{
							inQuotes = true;
							fieldWasQuoted = true;
						}
						else
						{
							field.Append(c);
						}

						break;

					case ',':
						columns.Add(field.ToString());
						field.Clear();
						fieldWasQuoted = false;
						break;

					case '\r':
						if (_reader.Peek() == '\n')
							_reader.Read();

						_line++;
						columns.Add(field.ToString());
						return true;

					case '\n':
						_line++;
						columns.Add(field.ToString());
						return true;

					default:
						field.Append(c);
						break;
				}
			}
		}

		public void Dispose()
		{
			_reader.Dispose();
		}
	}
}
