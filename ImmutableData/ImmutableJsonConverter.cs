using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmutableData
{
	public class ImmutableJsonConverter : JsonConverter
	{
		private ImmutableDatabase _immutables;
		private readonly List<Assembly> _assemblies;

		public ImmutableJsonConverter(ImmutableDatabase database, IAssemblyProvider assemblyProvider)
		{
			_immutables = database;
			_assemblies = assemblyProvider.GetAssemblies().ToList();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (!(value is Immutable immutable))
				throw new Exception("Should not happen but it did. Somehow serialization is running on object that is not supported.");
			
			var obj = new JObject();
			obj.Add("Type", JToken.FromObject(immutable.GetType().Name));
			obj.Add("Name", JToken.FromObject(immutable.Name));
			obj.WriteTo(writer);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var token = JToken.ReadFrom(reader);
			if (!token.HasValues) 
				return null;

			if (token["Type"] == null)
				return null;
			
			var type = token["Type"].ToString();
			var name = token["Name"].ToString();

			Type actualType = null;
			foreach (var assembly in _assemblies)
			{
				actualType = assembly.GetType(type);
				if (actualType != null)
					break;
			}

			if (actualType == null)
			{
				throw new Exception($"Could not find type {type} in any assembly.");
				// TODO: recycle here.
			}

			return _immutables.Get(actualType, name);
		}

		public override bool CanRead => true;

		public override bool CanConvert(Type objectType)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			if (typeof(AssetRef).IsAssignableFrom(objectType))
				throw new Exception($"Trying to serialize reference type {nameof(AssetRef)}. This is not supported.");
#pragma warning restore CS0618 // Type or member is obsolete
			
			return typeof(Immutable).IsAssignableFrom(objectType);
		}
	}
}