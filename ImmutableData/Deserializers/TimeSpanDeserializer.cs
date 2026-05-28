using System;
using System.Data;
using System.Text.RegularExpressions;

namespace ImmutableData.Deserializers
{
    public class TimeSpanDeserializer : IImmutableDeserializer
    {
        public bool CanDeserialize(Type type)
        {
            return typeof(TimeSpan).IsAssignableFrom(type) || typeof(TimeSpan?).IsAssignableFrom(type);
        }

        public bool Deserialize(Type type, string data, out object value)
        {
            var time = TimeSpan.Zero;
            var matches = Regex.Matches(data, "(\\d+)([dhsm]{1,2})");
            
            foreach (Match match in matches)
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
                value = (TimeSpan?) time;
            else
                value = time;
            
            return true;
        }
    }
}