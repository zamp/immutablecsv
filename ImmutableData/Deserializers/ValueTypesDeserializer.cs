using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ImmutableData.Utilities;

namespace ImmutableData.Deserializers
{
    public class ValueTypesDeserializer : IImmutableDeserializer
    {
	    private static readonly Regex UnitRegex = new(@"^(-?\d+\.?\d*)\s*(%|°\w+|\w+)?", RegexOptions.Compiled);
        
        private static readonly List<Type> SerializableTypes = new List<Type>
        {
            typeof(bool),
            typeof(int),
            typeof(int?),
            typeof(uint),
            typeof(uint?),
            typeof(float),
            typeof(float?),
            typeof(double),
            typeof(double?),
            typeof(long),
            typeof(long?),
            typeof(decimal),
            typeof(decimal?),
            typeof(string),
            typeof(Enum),
        };
        
        public bool CanDeserialize(Type type)
        {
            return SerializableTypes.Any(x => x.IsAssignableFrom(type));
        }

        public bool Deserialize(Type type, string data, out object value)
        {
            if (string.IsNullOrEmpty(data))
            {
                value = null;
                return true;
            }

            if (type == typeof(string))
            {
                value = data;
                return true;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            
            var match = UnitRegex.Match(data);
            if (match.Success && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                var unitValue = UnitUtility.Convert(match.Groups[2].Value, float.Parse(match.Groups[1].Value));
                value = Convert.ChangeType(unitValue, type);
                return true;
            }

            if (typeof(Enum).IsAssignableFrom(type))
            {
                if (data == ">=")
                    data = "GreaterOrEqual";
                else if (data == "<=")
                    data = "LessOrEqual";
                else if (data == ">")
                    data = "Greater";
                else if (data == "<")
                    data = "Less";
                else if (data == "==")
                    data = "Equal";
                else if (data == "!=")
                    data = "NotEqual";
                
                value = Enum.Parse(type, data);
                return true;
            }
            
            if (typeof(IConvertible).IsAssignableFrom(type))
            {
                try
                {
                    value = Convert.ChangeType(data, type);
                    return true;
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to convert {data} to {type.Name}", e);
                }
            }
            
            value = null;
            return false;
        }
    }
}