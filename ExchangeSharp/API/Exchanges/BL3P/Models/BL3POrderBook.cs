using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models
{
	// ReSharper disable once InconsistentNaming
	internal class BL3POrderBook
	{
		[JsonProperty("marketplace")]
		public string MarketSymbol { get; set; }

		[JsonProperty("asks")]
		public BL3POrderRequest[] Asks { get; set; }

		[JsonProperty("bids")]
		public BL3POrderRequest[] Bids { get; set; }
	}
}
