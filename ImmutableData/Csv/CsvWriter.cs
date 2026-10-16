#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ImmutableData.Csv
{
	/// <summary>Writes RFC 4180 CSV that <see cref="CsvReader"/> reads back: fields are quoted only when they need it.</summary>
	public static class CsvWriter
	{
		public static string Write(IEnumerable<IReadOnlyList<string>> rows)
		{
			var text = new StringBuilder();

			foreach (var row in rows)
				text.Append(string.Join(",", row.Select(Escape))).Append('\n');

			return text.ToString();
		}

		public static void WriteFile(string path, IEnumerable<IReadOnlyList<string>> rows)
		{
			File.WriteAllText(path, Write(rows), new UTF8Encoding(false));
		}

		public static string Escape(string field)
		{
			if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 && field.Trim() == field)
				return field;

			return "\"" + field.Replace("\"", "\"\"") + "\"";
		}
	}
}
