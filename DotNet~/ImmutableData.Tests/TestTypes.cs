#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace ImmutableData.Tests
{
	public sealed class TestAssemblyProvider : IAssemblyProvider
	{
		public IEnumerable<Assembly> GetAssemblies()
		{
			yield return typeof(TestAssemblyProvider).Assembly;
		}
	}

	public enum Comparison
	{
		Equal,
		NotEqual,
		Greater,
		GreaterOrEqual,
		Less,
		LessOrEqual,
	}

	public class Fruit : Immutable
	{
		public readonly int Calories = 0;
		public readonly Fruit? Pairing = null;
		public readonly ImmutableList<Fruit> Related = ImmutableList<Fruit>.Empty;
		public readonly TimeSpan RipeningTime = TimeSpan.Zero;
		public readonly float Weight = 0;
	}

	public class Basket : Immutable
	{
		public readonly ImmutableList<Fruit> Contents = ImmutableList<Fruit>.Empty;
		public readonly WeightedList<Fruit> Odds = new();
		public readonly ImmutableList<Check> Checks = ImmutableList<Check>.Empty;
	}

	public abstract class Check
	{
	}

	[Ordered]
	public class AtLeast : Check
	{
		[Order(0)] public readonly string Stat = "";
		[Order(1)] public readonly Comparison Operator = Comparison.GreaterOrEqual;
		[Order(2)] public readonly int Value = 0;
	}

	[Ordered]
	public class Always : Check
	{
	}

	[Ordered]
	public class Scored
	{
		[Order(0)] public readonly Check Check = null!;
		[Order(1)] public readonly float Score = 0;
	}

	public class Rule : Immutable
	{
		public readonly ImmutableList<Scored> Scores = ImmutableList<Scored>.Empty;
	}

	[NotImmutableRoot]
	public abstract class Labeled : Immutable
	{
	}

	public class Tool : Labeled
	{
	}

	public class Hammer : Tool
	{
	}

	public class Settings : Immutable
	{
		public readonly string Motto = "Keep calm";
		public readonly int Speed = 1;
		public readonly string Title = "";
		public readonly Fruit? Favourite = null;
	}

	public class Strict : Immutable
	{
		public readonly int Value = 0;

		public override bool Validate(Result result)
		{
			result.ErrorOnTrue(Value < 0, "Value must not be negative.");
			result.ErrorOnTrue(Value > 100, "Value must be at most 100.");
			return result.IsSuccess;
		}
	}
}
