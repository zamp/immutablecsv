#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImmutableData.Csv;
using NUnit.Framework;

namespace ImmutableData.Tests
{
	public sealed class ImporterTests
	{
		[SetUp]
		public void SetUp()
		{
			ImmutableImporter.AssemblyProvider = new TestAssemblyProvider();
		}

		private static ImmutableDatabase Import(params (string Source, string Text)[] files)
		{
			var rows = files.SelectMany(x => CsvRowReader.ReadText(x.Text, x.Source));
			return ImmutableImporter.Import(rows);
		}

		[Test]
		public void ImportsValuesReferencesListsAndDurations()
		{
			var db = Import(("Fruits.csv",
				"Class,Name,Calories,Pairing,Related,RipeningTime,Weight\n" +
				"Fruit,Apple,52,Pear,\"Pear,Plum\",3d12h,0.2\n" +
				"Fruit,Pear,57,,,1d,150g\n" +
				"Fruit,Plum,46,Apple,[Apple],90m,\n"));

			var apple = db.Get<Fruit>("Apple");
			Assert.That(apple.Calories, Is.EqualTo(52));
			Assert.That(apple.Pairing, Is.SameAs(db.Get<Fruit>("Pear")));
			Assert.That(apple.Related.Select(x => x.Name), Is.EqualTo(new[] { "Pear", "Plum" }));
			Assert.That(apple.RipeningTime, Is.EqualTo(TimeSpan.FromHours(84)));
			Assert.That(db.Get<Fruit>("Pear").Weight, Is.EqualTo(0.15f).Within(0.0001f));
			Assert.That(db.Get<Fruit>("Plum").RipeningTime, Is.EqualTo(TimeSpan.FromMinutes(90)));
			Assert.That(db.GetAll<Fruit>().Count(), Is.EqualTo(3));
		}

		[Test]
		public void ImportsKeyValueConfig()
		{
			var db = Import(
				("Fruits.csv", "Class,Name\nFruit,Apple\n"),
				("Settings.csv", "Key,Value\nClass,Settings\nSpeed,3\nTitle,\"Hello, world\"\nFavourite,Apple\n"));

			var settings = db.Get<Settings>();
			Assert.That(settings.Speed, Is.EqualTo(3));
			Assert.That(settings.Title, Is.EqualTo("Hello, world"));
			Assert.That(settings.Favourite!.Name, Is.EqualTo("Apple"));
		}

		[Test]
		public void KeyValueRowWithExtraCellsFails()
		{
			var error = Assert.Throws<InvalidDataException>(() => Import(("Settings.csv", "Key,Value\nClass,Settings\nSpeed,7,99\n")))!;

			Assert.That(error.Message, Does.Contain("Settings.csv:3"));
			Assert.That(error.Message, Does.Contain("quote the value"));
		}

		[Test]
		public void SubclassOfAClassBelowANotImmutableRootSharesItsNames()
		{
			var db = Import(("Tools.csv", "Class,Name\nHammer,Claw\n"));

			Assert.That(ImmutableDatabase.GetRootBaseType(typeof(Hammer)), Is.EqualTo(typeof(Tool)));
			Assert.That(db.TryGet<Tool>("Claw", out var tool), Is.True);
			Assert.That(tool, Is.InstanceOf<Hammer>());
			Assert.Throws<ImmutableImportException>(() => Import(("Tools.csv", "Class,Name\nTool,Claw\nHammer,Claw\n")));
		}

		[Test]
		public void EmptyPartOfAnOrderedValueKeepsTheDefault()
		{
			var db = Import(("Baskets.csv", "Class,Name,Checks\nBasket,B,\"AtLeast:Sweetness||5\"\n"));

			var check = (AtLeast)db.Get<Basket>("B").Checks[0];
			Assert.That(check.Operator, Is.EqualTo(Comparison.GreaterOrEqual));
			Assert.That(check.Value, Is.EqualTo(5));
		}

		[Test]
		public void OrderedValueMissingARequiredReferenceFails()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import(("Rules.csv", "Class,Name,Scores\nRule,R,\"[Always]|5,Scored\"\n")))!;

			Assert.That(error.Message, Does.Contain("Rule:R").And.Contain("Scores[1] Scored: Check is required."));
		}

		[Test]
		public void ValidatableOrderedValueChecksItself()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import(("Baskets.csv", "Class,Name,Checks\nBasket,B,\"AtLeast:|>=|5\"\n")))!;

			Assert.That(error.Message, Does.Contain("Checks[0] AtLeast: Stat is empty."));
		}

		[Test]
		public void EmptyCellsKeepTheClassDefault()
		{
			var db = Import(("Settings.csv", "Class,Name,Motto,Speed\nSettings,,,\n"));

			Assert.That(db.Get<Settings>().Motto, Is.EqualTo("Keep calm"));
			Assert.That(db.Get<Settings>().Speed, Is.EqualTo(1));
		}

		[Test]
		public void ImportsOrderedTypesWeightedListsAndOperators()
		{
			var db = Import(
				("Fruits.csv", "Class,Name\nFruit,Apple\nFruit,Pear\n"),
				("Baskets.csv", "Class,Name,Contents,Odds,Checks\nBasket,Mixed,\"Apple,Pear\",\"Apple|3,Pear|1\",\"AtLeast:Sweetness|>=|5,Always:\"\n"));

			var basket = db.Get<Basket>("Mixed");
			Assert.That(basket.Odds.GetWeight(db.Get<Fruit>("Apple")), Is.EqualTo(3));
			Assert.That(basket.Checks.Count, Is.EqualTo(2));

			var atLeast = (AtLeast)basket.Checks[0];
			Assert.That(atLeast.Stat, Is.EqualTo("Sweetness"));
			Assert.That(atLeast.Operator, Is.EqualTo(Comparison.GreaterOrEqual));
			Assert.That(atLeast.Value, Is.EqualTo(5));
			Assert.That(basket.Checks[1], Is.InstanceOf<Always>());
		}

		[Test]
		public void OrderedTypeNameAloneUsesDefaultsAndValuesMayContainColons()
		{
			var db = Import(
				("Fruits.csv", "Class,Name\nFruit,Apple\n"),
				("Baskets.csv", "Class,Name,Checks\nBasket,Plain,\"Always,AtLeast:Time:10|==|1\"\n"));

			var checks = db.Get<Basket>("Plain").Checks;
			Assert.That(checks[0], Is.InstanceOf<Always>());
			Assert.That(((AtLeast)checks[1]).Stat, Is.EqualTo("Time:10"));
		}

		[Test]
		public void NestedOrderedValuesInBrackets()
		{
			var db = Import(("Rules.csv", "Class,Name,Scores\nRule,R,\"[Always:]|5,[AtLeast:Sweetness|>|3]|7,[Always]|9\"\n"));
			var scores = db.Get<Rule>("R").Scores;

			Assert.That(scores.Select(x => x.Score), Is.EqualTo(new[] { 5f, 7f, 9f }));
			Assert.That(scores[0].Check, Is.InstanceOf<Always>());
			Assert.That(((AtLeast)scores[1].Check).Value, Is.EqualTo(3));
			Assert.That(scores[2].Check, Is.InstanceOf<Always>());
		}

		[Test]
		public void DeserializeParsesASingleValueAgainstALoadedDatabase()
		{
			var db = Import(("Fruits.csv", "Class,Name\nFruit,Apple\n"));

			var related = ImmutableImporter.Deserialize<System.Collections.Immutable.ImmutableList<Fruit>>("Apple", db);
			Assert.That(related.Single(), Is.SameAs(db.Get<Fruit>("Apple")));
			Assert.Throws<ImmutableImportException>(() => ImmutableImporter.Deserialize<Fruit>("Nope", db));
			Assert.Throws<ImmutableImportException>(() => ImmutableImporter.Deserialize<int>("", db));
			Assert.That(ImmutableImporter.Deserialize<int?>("", db), Is.Null);
		}

		[Test]
		public void UnknownReferenceNamesFileRowAndColumn()
		{
			var error = Assert.Throws<RanOutOfResolveStepsException>(() => Import(("Fruits.csv",
				"Class,Name,Pairing\n" +
				"Fruit,Apple,\n" +
				"Fruit,Pear,Bananna\n")))!;

			Assert.That(error.Message, Does.Contain("Fruits.csv:3"));
			Assert.That(error.Message, Does.Contain("column Pairing"));
			Assert.That(error.Message, Does.Contain("no Fruit named 'Bananna'"));
		}

		[Test]
		public void BadValueNamesFileRowAndColumn()
		{
			var error = Assert.Throws<ImmutableImportException>(() => Import(("Fruits.csv", "Class,Name,RipeningTime\nFruit,Apple,soon\n")))!;

			Assert.That(error.Message, Does.Contain("Fruits.csv:2"));
			Assert.That(error.Message, Does.Contain("column RipeningTime = 'soon'"));
		}

		[Test]
		public void UnknownColumnFails()
		{
			var error = Assert.Throws<ImmutableImportException>(() => Import(("Fruits.csv", "Class,Name,Colour\nFruit,Apple,red\n")))!;

			Assert.That(error.Message, Does.Contain("Fruits.csv:2"));
			Assert.That(error.Message, Does.Contain("'Colour'"));
		}

		[Test]
		public void NameConflictNamesBothRows()
		{
			var error = Assert.Throws<ImmutableImportException>(() => Import(("A.csv", "Class,Name\nFruit,Apple\n"), ("B.csv", "Class,Name\nFruit,Apple\n")))!;

			Assert.That(error.Message, Does.Contain("A.csv:2"));
			Assert.That(error.Message, Does.Contain("B.csv:2"));
		}

		[Test]
		public void ValidationReportsEveryError()
		{
			var error = Assert.Throws<ImmutableImporter.ImmutableValidationException>(() => Import(("Strict.csv", "Class,Name,Value\nStrict,Low,-1\nStrict,Fine,5\nStrict,High,101\n")))!;

			Assert.That(error.Message, Does.Contain("Strict:Low: Value must not be negative."));
			Assert.That(error.Message, Does.Contain("Strict:High: Value must be at most 100."));
		}

		[Test]
		public void ImportsMoreThanAThousandRows()
		{
			var text = new StringBuilder("Class,Name,Pairing\n");
			for (var i = 0; i < 2500; ++i)
				text.Append($"Fruit,F{i},F{(i + 1) % 2500}\n");

			var db = Import(("Many.csv", text.ToString()));

			Assert.That(db.GetAll<Fruit>().Count(), Is.EqualTo(2500));
			Assert.That(db.Get<Fruit>("F2499").Pairing!.Name, Is.EqualTo("F0"));
		}

		[Test]
		public void TryGetDoesNotThrowForMissingNames()
		{
			var db = Import(("Fruits.csv", "Class,Name\nFruit,Apple\n"));

			Assert.That(db.TryGet<Fruit>("Nope", out var missing), Is.False);
			Assert.That(missing, Is.Null);
			Assert.That(db.TryGet<Fruit>("Apple", out var apple), Is.True);
			Assert.That(apple!.Name, Is.EqualTo("Apple"));
		}

		[Test]
		public void AssetRefCarriesNameAndTypeAndResolvesThroughTheHost()
		{
			var deserializer = new Deserializers.AssetRefDeserializer();
			deserializer.Deserialize(typeof(AssetRef<string>), "models/fridge.glb", out var value);
			var assetRef = (AssetRef<string>)value;

			Assert.That(assetRef.Name, Is.EqualTo("models/fridge.glb"));
			Assert.That(assetRef.AssetType, Is.EqualTo(typeof(string)));

#pragma warning disable CS0618
			AssetRef.Resolver = new EchoResolver();
			Assert.That(assetRef.Get(), Is.EqualTo("String:models/fridge.glb"));
			AssetRef.Resolver = null;
#pragma warning restore CS0618
		}

		private sealed class EchoResolver : IAssetResolver
		{
			public object? Resolve(Type assetType, string name)
			{
				return $"{assetType.Name}:{name}";
			}
		}
	}
}
