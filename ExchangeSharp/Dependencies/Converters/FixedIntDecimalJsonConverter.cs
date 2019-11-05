using System;
using ExchangeSharp.Utility;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	public class FixedIntDecimalJsonConverter : JsonConverter
	{
		private readonly FixedIntDecimalConverter converter;

		public FixedIntDecimalJsonConverter()
			: this(1)
		{
		}

		public FixedIntDecimalJsonConverter(int multiplier)
		{
			converter = new FixedIntDecimalConverter(multiplier);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var valueLong = converter.FromDecimal((decimal) value);
			writer.WriteValue(valueLong);
		}

		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		)
		{
			var valueLong = Convert.ToInt64(reader.Value);
			return converter.ToDecimal(valueLong);
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(decimal)
				|| objectType == typeof(long);
		}

		public override bool CanRead { get; } = true;

		public override bool CanWrite { get; } = true;
	}
}
