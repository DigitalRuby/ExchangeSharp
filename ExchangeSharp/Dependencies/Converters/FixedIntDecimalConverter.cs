using System;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	public class FixedIntDecimalConverter : JsonConverter
	{
		private readonly decimal dec;

		public FixedIntDecimalConverter()
		{
		}

		public FixedIntDecimalConverter(int dec)
		{
			this.dec = decimal.Parse(1.ToString().PadRight(dec + 1, '0'));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		)
		{
			var valueInt = Convert.ToInt64(reader.Value);
			return valueInt / dec;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(decimal);
		}

		public override bool CanRead { get; } = true;

		public override bool CanWrite { get; } = false;
	}
}
