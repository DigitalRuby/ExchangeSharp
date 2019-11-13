using System;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		/// <summary>
		/// For use in SubscribeLevel1 OnGetTickersWebSocketAsync()
		/// </summary>
		class Level1Data
		{
			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			[JsonProperty("InstrumentId")]
			public long InstrumentId { get; set; }

			[JsonProperty("BestBid")]
			public long? BestBid { get; set; }

			[JsonProperty("BestOffer")]
			public long? BestOffer { get; set; }

			[JsonProperty("LastTradedPx")]
			public double LastTradedPx { get; set; }

			[JsonProperty("LastTradedQty")]
			public double LastTradedQty { get; set; }

			[JsonProperty("LastTradeTime")]
			public long? LastTradeTime { get; set; }

			[JsonProperty("SessionOpen")]
			public long? SessionOpen { get; set; }

			[JsonProperty("SessionHigh")]
			public long? SessionHigh { get; set; }

			[JsonProperty("SessionLow")]
			public long? SessionLow { get; set; }

			[JsonProperty("SessionClose")]
			public double SessionClose { get; set; }

			[JsonProperty("Volume")]
			public decimal? Volume { get; set; }

			[JsonProperty("CurrentDayVolume")]
			public long? CurrentDayVolume { get; set; }

			[JsonProperty("CurrentDayNumTrades")]
			public long? CurrentDayNumTrades { get; set; }

			[JsonProperty("CurrentDayPxChange")]
			public long? CurrentDayPxChange { get; set; }

			[JsonProperty("Rolling24HrVolume")]
			public long? Rolling24HrVolume { get; set; }

			[JsonProperty("Rolling24NumTrades")]
			public long? Rolling24NumTrades { get; set; }

			[JsonProperty("Rolling24HrPxChange")]
			public long? Rolling24HrPxChange { get; set; }

			[JsonProperty("TimeStamp")]
			public string TimeStamp { get; set; }

			public ExchangeTicker ToExchangeTicker(string currencyPair)
			{
				var currencyParts = currencyPair.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries);
				return new ExchangeTicker()
				{
					Bid = BestBid.GetValueOrDefault(),
					Ask = BestOffer.GetValueOrDefault(),
					Id = InstrumentId.ToString(),
					Last = LastTradeTime.GetValueOrDefault(),
					Volume = new ExchangeVolume()
					{
						BaseCurrency = currencyParts[0],
						QuoteCurrency = currencyParts[1],
						BaseCurrencyVolume = Volume.GetValueOrDefault()
					}
				};
			}
		}
    }
}
