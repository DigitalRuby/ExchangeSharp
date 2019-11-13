using System;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class Instrument
		{
			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			[JsonProperty("InstrumentId")]
			public long InstrumentId { get; set; }

			[JsonProperty("Symbol")]
			public string Symbol { get; set; }

			[JsonProperty("Product1")]
			public long Product1 { get; set; }

			[JsonProperty("Product1Symbol")]
			public string Product1Symbol { get; set; }

			[JsonProperty("Product2")]
			public long Product2 { get; set; }

			[JsonProperty("Product2Symbol")]
			public string Product2Symbol { get; set; }

			[JsonProperty("InstrumentType")]
			public string InstrumentType { get; set; }

			[JsonProperty("VenueInstrumentId")]
			public long VenueInstrumentId { get; set; }

			[JsonProperty("VenueId")]
			public long VenueId { get; set; }

			[JsonProperty("SortIndex")]
			public long SortIndex { get; set; }

			[JsonProperty("SessionStatus")]
			public string SessionStatus { get; set; }

			[JsonProperty("PreviousSessionStatus")]
			public string PreviousSessionStatus { get; set; }

			[JsonProperty("SessionStatusDateTime")]
			public DateTimeOffset SessionStatusDateTime { get; set; }

			[JsonProperty("SelfTradePrevention")]
			public bool SelfTradePrevention { get; set; }

			[JsonProperty("QuantityIncrement")]
			public double QuantityIncrement { get; set; }

			[JsonProperty("PriceIncrement")]
			public double PriceIncrement { get; set; }

			public ExchangeMarket ToExchangeMarket()
			{
				return new ExchangeMarket()
				{
					BaseCurrency = Product1Symbol,
					IsActive = SessionStatus.Equals("running", StringComparison.InvariantCultureIgnoreCase),
					MarginEnabled = false,
					MarketId = InstrumentId.ToStringInvariant(),
					MarketSymbol = Symbol,
					AltMarketSymbol = InstrumentId.ToStringInvariant(),
				};
			}
		}
    }
}
