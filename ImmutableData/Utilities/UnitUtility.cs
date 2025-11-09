using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmutableData.Utilities
{
	public static class UnitUtility
	{
		// NOTICE! If you add more make them lowercase!
		// NOTICE! If you add more make them lowercase!
		// NOTICE! If you add more make them lowercase!
		
		private static readonly Dictionary<string, double> Special = new()
		{
			{"%", 0.01},
			{"pi", Math.PI},
			{"π", Math.PI}
		};
		
		private static readonly Dictionary<string, double> Mass = new()
		{
			{"g", 0.001},      // gram
			{"kg", 1},         // kilogram
			{"t", 1e3},        // tonne
			{"kt", 1e6},       // kilotonne
			{"Mt", 1e9},       // megatonne
			{"Gt", 1e12},      // gigatonne
			{"Tt", 1e15},      // teratonne
			{"Tg", 1e12},      // teragram
			{"Pg", 1e15},      // petagram
			{"Eg", 1e18},      // exagram
			{"Zg", 1e21},      // zettagram
			{"Yg", 1e24},      // yottagram
		};
		
		private static readonly Dictionary<string, double> Pressure = new()
		{
			{"Pa", 1},               // Pascal
			{"kPa", 1e3},            // Kilopascal
			{"bar", 1e5},            // Bar
			{"MPa", 1e6},            // Megapascal
			{"GPa", 1e9},            // Gigapascal
		};
		
		private static readonly Dictionary<string, double> Distance = new()
		{
			{"mm", 0.001},			 // millimeter
			{"cm", 0.01},			 // centimeter
			{"m", 1},			     // meter
			{"km", 1000},			 // kilometer
			{"Ls", 299792458},		 // light seconds
			{"Ly", 9460730472580800} // light years
		};
		
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
			if (Mass.TryGetValue(unit, out multiplier))
				return value * multiplier;
			if (Distance.TryGetValue(unit, out multiplier))
				return value * multiplier;
			if (Temperature.TryGetValue(unit, out var conversion))
				return conversion(value);
			return value;
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToMassString(this double value)
		{
			var list = Mass.Select(@decimal => $"{TruncateDouble(value / @decimal.Value)} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToPressureString(this double value)
		{
			var list = Pressure.Select(@decimal => $"{TruncateDouble(value / @decimal.Value)} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}
		
		/// <summary>
		/// Returns a value with least digits
		/// </summary>
		public static string ToDistanceString(this double value)
		{
			var list = Distance.Select(@decimal => $"{TruncateDouble(value / @decimal.Value)} {@decimal.Key}").ToList();
			list.Sort(LengthSort);
			return list[0];
		}
		
		public static string TruncateDouble(double value)
		{
			// If value >= 1, return without decimals
			if (value >= 1)
			{
				return ((int)value).ToString();
			}
    
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