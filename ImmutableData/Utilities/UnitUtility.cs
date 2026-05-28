using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Utilities
{
	public static class UnitUtility
	{
		public static readonly Dictionary<string, double> Special = new()
		{
			{"%", 0.01},
			{"pi", Math.PI},
			{"π", Math.PI}
		};

		public static readonly Dictionary<string, (double kg, double minDisplay)> Mass = new()
		{
			{ "g", (0.001, 1) }, // gram
			{ "kg", (1, 1) }, // kilogram
			{ "t", (1e3, 1) }, // tonne
			{ "kt", (1e6, 1) }, // kilotonne
			{ "Mt", (1e9, 1) }, // megatonne
			{ "Gt", (1e12, 1) }, // gigatonne
			{ "Tt", (1e15, 1) }, // teratonne
			{ "Tg", (1e12, 1) }, // teragram
			{ "Pg", (1e15, 1) }, // petagram
			{ "Eg", (1e18, 1) }, // exagram
			{ "Zg", (1e21, 1) }, // zettagram
			{ "Yg", (1e24, 1) }, // yottagram
		};

		public static readonly Dictionary<string, (double pascal, double minDisplay)> Pressure = new()
		{
			{ "Pa", (1, 1) }, // Pascal
			{ "kPa", (1e3, 1) }, // Kilopascal
			{ "bar", (1e5, 1) }, // Bar
			{ "MPa", (1e6, 1) }, // Megapascal
			{ "GPa", (1e9, 1) }, // Gigapascal
		};

		public static readonly Dictionary<string, (double meters, double minDisplay)> Distance = new()
		{
			{ "mm", (0.001, 1) }, // millimeter
			{ "cm", (0.01, 1) }, // centimeter
			{ "m", (1, 1) }, // meter
			{ "km", (1000, 5) }, // kilometer
			{ "Mm", (10000000, 10) }, // megameter
			{ "Ls", (299792458, 0.1) }, // light seconds
			{ "Lm", (17987547480, 1) }, // light minutes
			{ "Lh", (1079252848800, 1) }, // light hours
			{ "Ld", (25902068371200, 1) }, // light days
			{ "Ly", (9460730472580800, 1) } // light years
		};
		
		public static readonly KeyValuePair<string, (double kg, double minDisplay)>[] MassOrdered = Mass.OrderBy(d => d.Value).ToArray();
		public static readonly KeyValuePair<string, (double pascal, double minDisplay)>[] PressureOrdered = Pressure.OrderBy(d => d.Value).ToArray();
		public static readonly KeyValuePair<string, (double meters, double minDisplay)>[] DistanceOrdered = Distance.OrderBy(d => d.Value).ToArray();
		
		private static readonly Dictionary<string, Func<double, double>> Temperature = new()
		{
			{"°C", c => c + 273.15},				// celsius to kelvin
			{"°F", f => (f - 32) * 5/9 + 273.15},	// fahrenheit to kelvin
			{"°K", k => k},							// kelvin (already in kelvin)
		};

		public static double Convert(string unit, double value)
		{
			if (Special.TryGetValue(unit, out var multiplier))
				return value * multiplier;
			if (Mass.TryGetValue(unit, out var mass))
				return value * mass.kg;
			if (Distance.TryGetValue(unit, out var distance))
				return value * distance.meters;
			if (Temperature.TryGetValue(unit, out var conversion))
				return conversion(value);
			if (Pressure.TryGetValue(unit, out var pressure))
				return value * pressure.pascal;
			return value;
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToMassString(this double value)
		{
			string result = null;
			foreach (var (symbol, mass) in MassOrdered)
			{
				var converted = Math.Round(value / mass.kg, 2);
				if (converted >= mass.minDisplay)
					result = $"{converted} {symbol}";
			}
			return result;
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToPressureString(this double value)
		{
			string result = null;
			foreach (var (symbol, pressure) in PressureOrdered)
			{
				var converted = Math.Round(value / pressure.pascal, 2);
				if (converted >= pressure.minDisplay)
					result = $"{converted} {symbol}";
			}
			return result;
		}
		
		public static string ToDistanceString(this double value)
		{
			string result = null;
			foreach (var (symbol, distance) in DistanceOrdered)
			{
				var converted = Math.Round(value / distance.meters, 2);
				if (converted >= distance.minDisplay)
					result = $"{converted} {symbol}";
			}
			return result;
		}
		
		public static string TruncateDouble(double value)
		{
			// If value >= 1, return without decimals
			/*if (value >= 1)
			{
				return ((int)value).ToString();
			}*/
    
			// Handle zero
			if (value == 0)
			{
				return "0";
			}
    
			// Convert to string with high precision
			var strValue = value.ToString("0.####################");
    
			// Find the decimal point
			var decimalIndex = strValue.IndexOf('.');
			if (decimalIndex == -1)
			{
				return strValue;
			}
    
			// Find first non-zero digit after decimal
			for (var i = decimalIndex + 1; i < strValue.Length; i++)
			{
				if (strValue[i] != '0')
				{
					// Truncate at this position (include this digit)
					return strValue.Substring(0, i + 1);
				}
			}
    
			return strValue.TrimEnd('0').TrimEnd('.');
		}

		private static int LengthSort(string a, string b)
		{
			var leadZeroA = a[0] == '0';
			var leadZeroB = b[0] == '0';
			
			if (leadZeroA && leadZeroB)
				return a.Length.CompareTo(b.Length);
			if (leadZeroA)
				return 1;
			if (leadZeroB)
				return -1;
			return a.Length.CompareTo(b.Length);
		}
	}
}