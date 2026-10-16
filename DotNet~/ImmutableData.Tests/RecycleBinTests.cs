#nullable enable

using System.Collections.Generic;
using System.Linq;
using ImmutableData.Csv;
using NUnit.Framework;

namespace ImmutableData.Tests
{
	public sealed class RecycleBinTests
	{
		[SetUp]
		public void SetUp()
		{
			ImmutableImporter.AssemblyProvider = new TestAssemblyProvider();
		}

		private static ImmutableDatabase Import(string active, string recycled)
		{
			var rows = new List<ImmutableRow>();
			rows.AddRange(CsvRowReader.ReadText(active, "data/Fruits.csv"));
			rows.AddRange(CsvRowReader.ReadText(recycled, "data/recycle/Fruits.csv", recycleBin: true));
			return ImmutableImporter.Import(rows);
		}

		[Test]
		public void RenamedRowResolvesToItsReplacement()
		{
			var db = Import("Class,Name,Calories\nFruit,GreenApple,52\n", "Class,Name,ReplacedBy,RemovedIn\nFruit,Apple,GreenApple,0.2.0\n");

			Assert.That(db.Get<Fruit>("Apple"), Is.SameAs(db.Get<Fruit>("GreenApple")));
			Assert.That(db.GetAll<Fruit>().Count(), Is.EqualTo(1));
		}

		[Test]
		public void ReplacedByChainsAreFollowed()
		{
			var db = Import("Class,Name\nFruit,C\n", "Class,Name,ReplacedBy\nFruit,A,B\nFruit,B,C\n");

			Assert.That(db.Get<Fruit>("A").Name, Is.EqualTo("C"));
		}

		[Test]
		public void ActiveDataReferencingARenamedRowGetsTheReplacement()
		{
			var db = Import("Class,Name,Pairing\nFruit,GreenApple,\nFruit,Pear,Apple\n", "Class,Name,ReplacedBy\nFruit,Apple,GreenApple\n");

			Assert.That(db.Get<Fruit>("Pear").Pairing!.Name, Is.EqualTo("GreenApple"));
		}

		[Test]
		public void RetiredRowsLoadButAreNotListed()
		{
			var db = Import("Class,Name\nFruit,Apple\n", "Class,Name,Calories,RemovedIn\nFruit,Durian,147,0.3.0\n");

			var durian = db.Get<Fruit>("Durian");
			Assert.That(durian.IsRetired, Is.True);
			Assert.That(durian.Calories, Is.EqualTo(147));
			Assert.That(durian.RemovedIn, Is.EqualTo("0.3.0"));
			Assert.That(db.GetAll<Fruit>().Select(x => x.Name), Is.EqualTo(new[] { "Apple" }));
			Assert.That(db.GetRetired<Fruit>().Single(), Is.SameAs(durian));
			Assert.That(db.IsActive(typeof(Fruit), "Durian"), Is.False);
		}

		[Test]
		public void RetiredRowsKeepDefaultsForReferencesThatAreGone()
		{
			var db = Import("Class,Name\nFruit,Apple\n", "Class,Name,Pairing\nFruit,Durian,Jackfruit\n");

			Assert.That(db.Get<Fruit>("Durian").Pairing, Is.Null);
			Assert.That(db.Warnings.Single(), Does.Contain("no Fruit named 'Jackfruit'"));
		}

		[Test]
		public void NameBothActiveAndRecycledFails()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import("Class,Name\nFruit,Apple\n", "Class,Name\nFruit,Apple\n"))!;

			Assert.That(error.Message, Does.Contain("both active and in the recycle bin"));
		}

		[Test]
		public void ReplacedByLoopFails()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import("Class,Name\nFruit,Apple\n", "Class,Name,ReplacedBy\nFruit,A,B\nFruit,B,A\n"))!;

			Assert.That(error.Message, Does.Contain("loop"));
		}

		[Test]
		public void ReplacedByUnknownNameFails()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import("Class,Name\nFruit,Apple\n", "Class,Name,ReplacedBy\nFruit,Pear,Quince\n"))!;

			Assert.That(error.Message, Does.Contain("does not exist"));
		}
	}
}
