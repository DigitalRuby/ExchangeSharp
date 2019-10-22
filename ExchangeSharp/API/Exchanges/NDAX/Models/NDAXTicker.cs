using System;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class NDAXTicker
		{
			[JsonProperty("isFrozen")]
			[JsonConverter(typeof(BoolConverter))]
			public bool IsFrozen { get; set; }

			[JsonProperty("lowestAsk")] public decimal? LowestAsk { get; set; }
			[JsonProperty("highestBid")] public decimal? HighestBid { get; set; }
			[JsonProperty("last")] public decimal? Last { get; set; }
			[JsonProperty("high24hr")] public decimal? High24Hr { get; set; }
			[JsonProperty("low24hr")] public decimal? Low24Hr { get; set; }
			[JsonProperty("id")] public long Id { get; set; }
			[JsonProperty("percentChange")] public decimal? PercentChange { get; set; }
			[JsonProperty("baseVolume")] public decimal? BaseVolume { get; set; }
			[JsonProperty("quoteVolume")] public decimal? QuoteVolume { get; set; }

			public ExchangeTicker ToExchangeTicker(string currencyPair)
			{
				var currencyParts = currencyPair.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries);
				return new ExchangeTicker()
				{
					MarketSymbol = currencyPair,
					Ask = LowestAsk.GetValueOrDefault(),
					Bid = HighestBid.GetValueOrDefault(),
					Id = Id.ToStringInvariant(),
					Last = Last.GetValueOrDefault(),
					Volume = new ExchangeVolume()
					{
						BaseCurrency = currencyParts[0],
						QuoteCurrency = currencyParts[1],
						BaseCurrencyVolume = BaseVolume.GetValueOrDefault(),
						QuoteCurrencyVolume = QuoteVolume.GetValueOrDefault()
					}
				};
			}
		}
	}
}
