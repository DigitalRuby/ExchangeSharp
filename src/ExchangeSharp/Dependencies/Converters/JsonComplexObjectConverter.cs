using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp.Dependencies.Converters
{
	/// <summary>
	/// Allows deserializing complex json objects
	/// </summary>
	/// <remarks>https://stackoverflow.com/a/8031283/4084610</remarks>
	public abstract class JsonComplexObjectConverter<T> : JsonConverter
	{
		public override bool CanRead => true;

		public override bool CanWrite => false;

		protected abstract T Create(JsonReader reader);

		public override bool CanConvert(Type objectType)
		{
			return typeof(T).IsAssignableFrom(objectType);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotSupportedException("This converted should not be used to write.");
		}

		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		)
		{
			var jObject = JObject.Load(reader);

			using var jsonReader = jObject.CreateReader();
			var target = Create(jsonReader);

			using var jsonReaderPopulate = jObject.CreateReader();
			serializer.Populate(jsonReaderPopulate, target);

			return target;
		}
	}
}
