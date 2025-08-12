using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Reflection;

namespace ImmutableData.Deserializers
{
    public class OrderedTypeDeserializer : IImmutableDeserializer
    {
        public static ImmutableDatabase ImmutableDatabase;
        
        public bool CanDeserialize(Type type)
        {
            return type.GetCustomAttribute<OrderedAttribute>() != null;
        }

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
            var parts = data.Split(':');
            var className = parts[0];
            data = parts.Length > 1 ? parts[1] : parts[0];

            var classType = type.Assembly.GetTypes().FirstOrDefault(x => x.Name == className);

            if (classType == null)
            {
                // throw error in case class is a list or array
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    throw new DataException($"No such class found {className}. Lists are not supported.");
                }
                
                if (type.IsArray)
                {
                    throw new DataException($"No such class found {className}. Arrays are not supported.");
                }

                classType = type;
            }

            var orderedProperties = classType.GetProperties()
                .Where(x => Attribute.IsDefined(x, typeof(OrderAttribute)))
                .ToDictionary(x => x.GetCustomAttributes().OfType<OrderAttribute>().First().Order, x => x);

            if (orderedProperties.Count == 0)
            {
                deserializedObject = Activator.CreateInstance(classType);
                return true;
            }

            var splitData = new List<string>();
            var split = data.Split(';');

            var blockStart = 0;
            var readingBlock = false;
            for (var i = 0; i < split.Length; ++i)
            {
                if (readingBlock && !split[i].EndsWith("]"))
                    continue;

                if (readingBlock)
                {
                    var str = string.Join(";", split, blockStart, i - blockStart + 1);
                    // trim brackets
                    str = str.Substring(1, str.Length - 2);
                    splitData.Add(str);
                    readingBlock = false;
                    continue;
                }

                if (split[i].StartsWith("["))
                {
                    blockStart = i;
                    readingBlock = true;
                }
                else
                {
                    splitData.Add(split[i]);
                }
            }

            deserializedObject = Activator.CreateInstance(classType);

            SetOrderedProperties(type, ref deserializedObject, orderedProperties, splitData);

            return true;
        }

        private static void SetOrderedProperties(Type type, ref object value, Dictionary<int, PropertyInfo> orderedProperties, List<string> splitData)
        {
            if (splitData.Count == 0)
                return;
            
            var j = 0;
            var skippedNullable = false;

            foreach (var orderedProperty in orderedProperties)
            {
                var nullableType = Nullable.GetUnderlyingType(orderedProperty.Value.PropertyType);
                var propertyType = nullableType ?? orderedProperty.Value.PropertyType;
                
                if (j >= splitData.Count)
                {
                    if (skippedNullable && nullableType == null)
                        throw new InvalidDataException($"Non nullable property defined after nullable property in '{type.Name}'. Nullable properties have to be ordered after non nullable properties.");
                    
                    skippedNullable = true;
                    continue;
                }
                
                var propertyData = splitData[j];

                if (nullableType == null)
                {
                    if (ImmutableImporter.TryDeserialize(propertyType, propertyData, out var obj))
                    {
                        if (orderedProperty.Value.SetMethod == null)
                        {
                            orderedProperty.Value.GetBackingField().SetValue(value, obj);
                        }
                        else
                        {
                            orderedProperty.Value.SetValue(value, obj);
                        }
                    }
                    else
                        throw new InvalidDataException($"Could not deserialize value '{propertyData}' for property {propertyType}.");
                }
                else if (ImmutableImporter.TryDeserialize(nullableType, propertyData, out var nullableObj))
                {
                    orderedProperty.Value.SetValue(value, nullableObj);
                }

                j++;
            }
        }
    }
}