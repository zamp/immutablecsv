using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Utilities
{
	public static class UnitUtility
	{
		private static readonly Dictionary<string, float> Special = new Dictionary<string, float>
		{
			{"%", 0.01f},
		};
		
		private static readonly Dictionary<string, float> Mass = new Dictionary<string, float>
		{
			{"kg", 1},
			{"g", 0.001f}
		};
		
		private static readonly Dictionary<string, float> Distance = new Dictionary<string, float>
		{
			{"mm", 0.001f},
			{"cm", 0.01f},
			{"km", 1000},
			{"au", 149597870700},
			{"ly", 9460730472580800}
		};

		public static float Convert(string unit, float value)
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
		public static string ToMassString(this float value)
		{
			var list = Mass.Select(@decimal => $"{value / @decimal.Value:F} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToDistanceString(this float value)
		{
			var list = Distance.Select(@decimal => $"{value / @decimal.Value:F} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}

		private static int LengthSort(string a, string b)
		{
			return a.Length.CompareTo(b.Length);
		}

		public static float GramsToKg(float grams)
		{
			return grams / 1000f;
		}
		
		public static float KgToGrams(float grams)
		{
			return grams * 1000f;
		}
	}
}