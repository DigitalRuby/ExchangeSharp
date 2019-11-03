using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeBL3PAPI : ExchangeAPI
	{
		// ReSharper disable once InconsistentNaming
		class BL3POrderBook
		{
			[JsonProperty("marketplace")]
			public string MarketSymbol { get; set; }

			[JsonProperty("asks")]
			public BL3POrder[] Asks { get; set; }

			[JsonProperty("bids")]
			public BL3POrder[] Bids { get; set; }
		}
	}
}
