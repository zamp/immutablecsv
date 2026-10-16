#nullable enable

using System.Collections.Generic;
using System.Linq;
using ImmutableData.Csv;
using NUnit.Framework;

namespace ImmutableData.Tests
{
	public sealed class RowLayerTests
	{
		[SetUp]
		public void SetUp()
		{
			ImmutableImporter.AssemblyProvider = new TestAssemblyProvider();
		}

		private static ImmutableDatabase Import(params (string Source, string Text)[] layers)
		{
			var rows = ImmutableRowLayers.Merge(layers.Select(x => CsvRowReader.ReadText(x.Text, x.Source)));
			return ImmutableImporter.Import(rows);
		}

		[Test]
		public void PatchChangesOnlyItsColumns()
		{
			var db = Import(
				("base/Fruits.csv", "Class,Name,Calories,RipeningTime,Weight\nFruit,Apple,52,3d,0.2\nFruit,Pear,57,1d,0.18\n"),
				("mod/Fruits.csv", "Class,Name,Calories\nFruit,Apple,60\n"));

			var apple = db.Get<Fruit>("Apple");
			Assert.That(apple.Calories, Is.EqualTo(60));
			Assert.That(apple.RipeningTime, Is.EqualTo(System.TimeSpan.FromDays(3)));
			Assert.That(apple.Weight, Is.EqualTo(0.2f));
			Assert.That(db.Get<Fruit>("Pear").Calories, Is.EqualTo(57));
		}

		[Test]
		public void EmptyPatchCellsChangeNothing()
		{
			var db = Import(
				("base/Fruits.csv", "Class,Name,Calories,Weight\nFruit,Apple,52,0.2\n"),
				("mod/Fruits.csv", "Class,Name,Calories,Weight\nFruit,Apple,,0.4\n"));

			Assert.That(db.Get<Fruit>("Apple").Calories, Is.EqualTo(52));
			Assert.That(db.Get<Fruit>("Apple").Weight, Is.EqualTo(0.4f));
		}

		[Test]
		public void PatchesStackInOrder()
		{
			var db = Import(
				("base/Fruits.csv", "Class,Name,Calories,Weight\nFruit,Apple,52,0.2\n"),
				("one/Fruits.csv", "Class,Name,Calories\nFruit,Apple,60\n"),
				("two/Fruits.csv", "Class,Name,Weight\nFruit,Apple,0.3\n"));

			Assert.That(db.Get<Fruit>("Apple").Calories, Is.EqualTo(60));
			Assert.That(db.Get<Fruit>("Apple").Weight, Is.EqualTo(0.3f));
		}

		[Test]
		public void PatchCanEditAConfig()
		{
			var db = Import(
				("base/Settings.csv", "Key,Value\nClass,Settings\nSpeed,3\nTitle,Base\n"),
				("mod/Settings.csv", "Key,Value\nClass,Settings\nSpeed,5\n"));

			Assert.That(db.Get<Settings>().Speed, Is.EqualTo(5));
			Assert.That(db.Get<Settings>().Title, Is.EqualTo("Base"));
		}

		[Test]
		public void RemoveDeletesAndNewRowsAdd()
		{
			var db = Import(
				("base/Fruits.csv", "Class,Name\nFruit,Apple\nFruit,Pear\n"),
				("mod/Fruits.csv", "Class,Name,Remove\nFruit,Pear,true\nFruit,Plum,\n"));

			Assert.That(db.GetAll<Fruit>().Select(x => x.Name).OrderBy(x => x), Is.EqualTo(new[] { "Apple", "Plum" }));
		}

		[Test]
		public void ErrorsPointAtThePatchThatWroteTheCell()
		{
			var error = Assert.Throws<RanOutOfResolveStepsException>(() => Import(
				("base/Fruits.csv", "Class,Name,Pairing\nFruit,Apple,\nFruit,Pear,Apple\n"),
				("mod/Fruits.csv", "Class,Name,Pairing\nFruit,Pear,Quince\n")))!;

			Assert.That(error.Message, Does.Contain("mod/Fruits.csv:2"));
			Assert.That(error.Message, Does.Contain("column Pairing"));
		}

		[Test]
		public void DuplicatesWithinOneLayerStillConflict()
		{
			Assert.Throws<ImmutableImportException>(() => Import(("base/Fruits.csv", "Class,Name\nFruit,Apple\nFruit,Apple\n")));
		}
	}
}
