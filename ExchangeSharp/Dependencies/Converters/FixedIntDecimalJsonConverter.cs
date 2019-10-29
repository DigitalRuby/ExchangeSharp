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
			var valueDecimal = converter.ToDecimal((long) value);
			writer.WriteValue((long) valueDecimal);
		}

		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		)
		{
			var valueDec = Convert.ToDecimal(reader.Value);
			return converter.FromDecimal(valueDec);
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(decimal);
		}

		public override bool CanRead { get; } = true;

		public override bool CanWrite { get; } = true;
	}
}
