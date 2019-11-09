using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3PAmount
	{
		[JsonProperty("value_int")] public long ValueInt { get; set; }

		[JsonProperty("display_short")] public string DisplayShort { get; set; }

		[JsonProperty("display")] public string Display { get; set; }

		[JsonProperty("currency")] public string Currency { get; set; }

		[JsonProperty("value")] public decimal Value { get; set; }
	}
}
