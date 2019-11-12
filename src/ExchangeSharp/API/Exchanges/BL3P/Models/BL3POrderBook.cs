using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	// ReSharper disable once InconsistentNaming
	internal class BL3POrderBook : BL3PResponsePayload
	{
		[JsonProperty("marketplace")]
		public string MarketSymbol { get; set; }

		[JsonProperty("asks")]
		public BL3POrderRequest[] Asks { get; set; }

		[JsonProperty("bids")]
		public BL3POrderRequest[] Bids { get; set; }
	}
}
