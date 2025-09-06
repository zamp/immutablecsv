using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Utilities
{
	public static class UnitUtility
	{
		private static readonly Dictionary<string, double> Special = new()
		{
			{"%", 0.01},
		};
		
		private static readonly Dictionary<string, double> Mass = new()
		{
			{"g", 0.001}, // gram
			{"kg", 1},    // kilogram
			{"t", 1e3},   // tonne
			{"Mt", 1e6},  // megatonne
			{"Gt", 1e9},  // gigatonne
			{"Tg", 1e12}, // teratonne
			{"Pg", 1e15}, // petatonne
			{"Eg", 1e18}, // exatonne
			{"Zg", 1e21}, // zettatonne
			{"Yg", 1e24}, // yottatonne
		};
		
		private static readonly Dictionary<string, double> Distance = new()
		{
			{"mm", 0.001},			 // millimeter
			{"cm", 0.01},			 // centimeter
			{"m", 1},			     // meter
			{"km", 1000},			 // kilometer
			{"ls", 299792458},		 // light seconds
			{"ly", 9460730472580800} // light years
		};

		public static double Convert(string unit, double value)
		{
			unit = unit.ToLowerInvariant();
			if (Special.TryGetValue(unit, out var multiplier))
				return value * multiplier;
			if (Mass.TryGetValue(unit, out multiplier))
				return value * multiplier;
			if (Distance.TryGetValue(unit, out multiplier))
				return value * multiplier;
			return value;
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToMassString(this double value)
		{
			var list = Mass.Select(@decimal => $"{value / @decimal.Value:F} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToDistanceString(this double value)
		{
			var list = Distance.Select(@decimal => $"{value / @decimal.Value:F} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}

		private static int LengthSort(string a, string b)
		{
			return a.Length.CompareTo(b.Length);
		}
	}
}