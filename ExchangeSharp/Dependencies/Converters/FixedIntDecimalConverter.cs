using System;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	public class FixedIntDecimalConverter : JsonConverter
	{
		private readonly decimal multiplier;

		public FixedIntDecimalConverter()
		{
		}

		public FixedIntDecimalConverter(int multiplier)
		{
			this.multiplier = decimal.Parse(1.ToString().PadRight(multiplier + 1, '0'));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var valueLong = (decimal) value * multiplier;
			writer.WriteValue((long) valueLong);
		}

		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		)
		{
			var valueDec = Convert.ToDecimal(reader.Value);
			return valueDec / multiplier;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(decimal);
		}

		public override bool CanRead { get; } = true;

		public override bool CanWrite { get; } = true;
	}
}
