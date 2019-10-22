using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models
{
	// ReSharper disable once InconsistentNaming
	public class BL3POrderBook
	{
		[JsonProperty("marketplace")]
		public string MarketSymbol { get; set; }

		[JsonProperty("asks")]
		public BL3POrder[] Asks { get; set; }

		[JsonProperty("bids")]
		public BL3POrder[] Bids { get; set; }
	}
}
