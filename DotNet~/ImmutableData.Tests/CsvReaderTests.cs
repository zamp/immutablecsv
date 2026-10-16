#nullable enable

using System.Collections.Generic;
using System.IO;
using ImmutableData.Csv;
using NUnit.Framework;

namespace ImmutableData.Tests
{
	public sealed class CsvReaderTests
	{
		private static List<List<string>> ReadAll(string text, List<int>? lines = null)
		{
			using var reader = new CsvReader(new StringReader(text));
			var rows = new List<List<string>>();
			var row = new List<string>();

			while (reader.ReadRow(row))
			{
				rows.Add(new List<string>(row));
				lines?.Add(reader.RowLine);
			}

			return rows;
		}

		[Test]
		public void ReadsQuotedFieldsWithCommasQuotesAndNewlines()
		{
			var rows = ReadAll("a,\"b,c\",\"say \"\"hi\"\"\"\n\"two\nlines\",x,\n");

			Assert.That(rows[0], Is.EqualTo(new[] { "a", "b,c", "say \"hi\"" }));
			Assert.That(rows[1], Is.EqualTo(new[] { "two\nlines", "x", "" }));
		}

		[Test]
		public void SkipsEmptyLinesAndTracksRowLines()
		{
			var lines = new List<int>();
			var rows = ReadAll("h1,h2\r\n\r\n1,2\r\n\"multi\nline\",3\n4,5", lines);

			Assert.That(rows.Count, Is.EqualTo(4));
			Assert.That(lines, Is.EqualTo(new[] { 1, 3, 4, 6 }));
		}

		[Test]
		public void WriterOutputReadsBackUnchanged()
		{
			var rows = new List<IReadOnlyList<string>>
			{
				new[] { "Class", "Name", "Note" },
				new[] { "Fruit", "Apple", "red, \"crisp\"\nand sweet" },
				new[] { "Fruit", " padded ", "" },
			};

			var read = ReadAll(CsvWriter.Write(rows));

			Assert.That(read, Is.EqualTo(rows));
		}

		[Test]
		public void UnterminatedQuoteFails()
		{
			Assert.Throws<InvalidDataException>(() => ReadAll("a,\"open\n"));
		}
	}
}
