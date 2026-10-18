using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using ImmutableData.Extensions;

namespace ImmutableData.Deserializers
{
    public class OrderedTypeDeserializer : IImmutableDeserializer
    {
        public static ImmutableDatabase ImmutableDatabase;
        
        public bool CanDeserialize(Type type)
        {
            if (type.GetCustomAttribute<OrderedAttribute>() != null)
                return true;

            // An abstract base (or interface) works too when some [Ordered] type implements it; the cell then names
            // the concrete type, as in "Above:100".
            if (!type.IsAbstract || typeof(Immutable).IsAssignableFrom(type))
                return false;

            lock (PolymorphicBases)
            {
                if (!PolymorphicBases.TryGetValue(type, out var isBase))
                {
                    isBase = type.Assembly.GetTypes().Any(x => !x.IsAbstract && type.IsAssignableFrom(x) && x.GetCustomAttribute<OrderedAttribute>() != null);
                    PolymorphicBases[type] = isBase;
                }

                return isBase;
            }
        }

        private static readonly Dictionary<Type, bool> PolymorphicBases = new();

        public bool Deserialize(Type type, string data, out object deserializedObject)
        {
            var originalData = data;
            var parts = data.Split(ImmutableImporter.TypeDelimiter);
            var className = parts[0];

            var classType = type.Assembly.GetTypes().FirstOrDefault(x => x.Name == className);

            // A colon-prefixed name only counts as a polymorphic type override if it actually
            // resolves to a type compatible with the field's declared type. Otherwise the colon
            // belongs to nested/inline data (e.g. a value embedded further down the field chain),
            // not a type tag, and must be left untouched for the caller to re-parse in full.
            if (classType != null && !type.IsAssignableFrom(classType))
                classType = null;

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
                data = originalData;
            }
            else
            {
                // Everything after the type name is the value; a bare type name ("Idle") means all defaults.
                var separator = originalData.IndexOf(ImmutableImporter.TypeDelimiter);
                data = separator < 0 ? "" : originalData.Substring(separator + 1);

                if (data.Length == 0)
                {
                    deserializedObject = Activator.CreateInstance(classType);
                    return true;
                }
            }

            var orderedProperties = classType.GetProperties()
                .Where(x => Attribute.IsDefined(x, typeof(OrderAttribute)))
                .ToDictionary(x => x.GetCustomAttributes().OfType<OrderAttribute>().First().Order, x => x);
            
            var orderedFields = classType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
	            .Where(x => Attribute.IsDefined(x, typeof(OrderAttribute)))
	            .ToDictionary(x => x.GetCustomAttributes().OfType<OrderAttribute>().First().Order, x => x);

            var hasProperties = orderedProperties.Count != 0;
            var hasFields = orderedFields.Count != 0; 
            if (!hasProperties && !hasFields)
            {
                deserializedObject = Activator.CreateInstance(classType);
                return true;
            }

            var splitData = new List<string>();
            var split = data.Split(ImmutableImporter.InlineDelimiter);

            var blockStart = 0;
            var readingBlock = false;
            for (var i = 0; i < split.Length; ++i)
            {
                if (readingBlock && !split[i].EndsWith(ImmutableImporter.ArrayEnd))
                    continue;

                if (readingBlock)
                {
                    var str = string.Join(ImmutableImporter.InlineDelimiter, split, blockStart, i - blockStart + 1);
                    // trim brackets
                    str = str.Substring(1, str.Length - 2);
                    splitData.Add(str);
                    readingBlock = false;
                    continue;
                }

                if (split[i].StartsWith(ImmutableImporter.ArrayStart) && split[i].EndsWith(ImmutableImporter.ArrayEnd))
                {
                    // A bracketed block without inner delimiters, such as [Type:value].
                    splitData.Add(split[i].Substring(1, split[i].Length - 2));
                }
                else if (split[i].StartsWith(ImmutableImporter.ArrayStart))
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

            if (hasProperties)
	            return TrySetOrderedProperties(type, ref deserializedObject, orderedProperties, splitData);
	        return TrySetOrderedFields(type, ref deserializedObject, orderedFields, splitData);
        }

        private static bool TrySetOrderedProperties(Type type, ref object value, Dictionary<int, PropertyInfo> orderedProperties, List<string> splitData)
        {
	        if (splitData.Count == 0)
		        return false;
            
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

                // an empty part keeps the class default, as an empty cell does
                if (string.IsNullOrWhiteSpace(propertyData))
                {
                    j++;
                    continue;
                }

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
                    {
	                    return false;
                    }
                }
                else if (ImmutableImporter.TryDeserialize(nullableType, propertyData, out var nullableObj))
                {
                    orderedProperty.Value.SetValue(value, nullableObj);
                }
                else
                {
	                return false;
                }

                j++;
            }

            return true;
        }
        
        private static bool TrySetOrderedFields(Type type, ref object value, Dictionary<int, FieldInfo> orderedFields, List<string> splitData)
        {
	        if (splitData.Count == 0)
		        return false;
            
            var j = 0;
            var skippedNullable = false;

            foreach (var orderedField in orderedFields)
            {
                var nullableType = Nullable.GetUnderlyingType(orderedField.Value.FieldType);
                var propertyType = nullableType ?? orderedField.Value.FieldType;
                
                if (j >= splitData.Count)
                {
                    if (skippedNullable && nullableType == null)
                        throw new InvalidDataException($"Non nullable property defined after nullable property in '{type.Name}'. Nullable properties have to be ordered after non nullable properties.");
                    
                    skippedNullable = true;
                    continue;
                }
                
                var propertyData = splitData[j];

                // an empty part keeps the class default, as an empty cell does
                if (string.IsNullOrWhiteSpace(propertyData))
                {
                    j++;
                    continue;
                }

                if (nullableType == null)
                {
	                if (ImmutableImporter.TryDeserialize(propertyType, propertyData, out var obj))
	                {
		                orderedField.Value.SetValue(value, obj);
	                }
	                else
	                {
		                return false;
	                }
                }
                else if (ImmutableImporter.TryDeserialize(nullableType, propertyData, out var nullableObj))
                {
                    orderedField.Value.SetValue(value, nullableObj);
                }
                else
                {
	                return false;
                }

                j++;
            }

            return true;
        }
    }
}