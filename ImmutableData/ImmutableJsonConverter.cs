using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImmutableData
{
	public class ImmutableJsonConverter : JsonConverter
	{
		private ImmutableDatabase _immutables;

		public ImmutableJsonConverter(ImmutableDatabase database)
		{
			_immutables = database;
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

			var actualType = _immutables.GetType().Assembly.GetType(type);

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