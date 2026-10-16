using System;
using System.Data;
using System.Text.RegularExpressions;

namespace ImmutableData.Deserializers
{
	/// <summary>
	/// Durations such as <c>5s</c>, <c>10m</c>, <c>2h</c>, <c>1d</c>, <c>250ms</c> or combinations like <c>1h30m</c>.
	/// The whole cell must be made of such parts; anything else is an error.
	/// </summary>
	public class TimeSpanDeserializer : IImmutableDeserializer
	{
		private static readonly Regex WholeRegex = new("^(\\d+(ms|d|h|m|s))+$", RegexOptions.Compiled);
		private static readonly Regex PartRegex = new("(\\d+)(ms|d|h|m|s)", RegexOptions.Compiled);

		public bool CanDeserialize(Type type)
		{
			return typeof(TimeSpan).IsAssignableFrom(type) || typeof(TimeSpan?).IsAssignableFrom(type);
		}

		public bool Deserialize(Type type, string data, out object value)
		{
			var compact = data.Replace(" ", "");

			if (!WholeRegex.IsMatch(compact))
				throw new DataException($"'{data}' is not a duration; use forms like 30s, 10m, 2h, 1d or 1h30m.");

			var time = TimeSpan.Zero;

			foreach (Match match in PartRegex.Matches(compact))
			{
				if (!int.TryParse(match.Groups[1].Value, out var number))
					throw new DataException($"Could not parse amount from time string {data}");

				switch (match.Groups[2].Value)
				{
					case "d":
						time += TimeSpan.FromDays(number);
						break;
					case "h":
						time += TimeSpan.FromHours(number);
						break;
					case "m":
						time += TimeSpan.FromMinutes(number);
						break;
					case "s":
						time += TimeSpan.FromSeconds(number);
						break;
					case "ms":
						time += TimeSpan.FromMilliseconds(number);
						break;
				}
			}

			if (type == typeof(TimeSpan?))
				value = (TimeSpan?)time;
			else
				value = time;

			return true;
		}
	}
}
