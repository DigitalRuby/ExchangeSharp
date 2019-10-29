using System;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		/// <summary>
		/// For use in GetTradesHistory: OnGetHistoricalTradesAsync()
		/// </summary>
		class TradeHistory
		{
			[JsonProperty("TradeTimeMS")]
			public long TradeTimeMs { get; set; }

			[JsonProperty("Fee")]
			public long Fee { get; set; }

			[JsonProperty("FeeProductId")]
			public long FeeProductId { get; set; }

			[JsonProperty("OrderOriginator")]
			public long OrderOriginator { get; set; }

			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			[JsonProperty("ExecutionId")]
			public long ExecutionId { get; set; }

			[JsonProperty("TradeId")]
			public long TradeId { get; set; }

			[JsonProperty("OrderId")]
			public long OrderId { get; set; }

			[JsonProperty("AccountId")]
			public long AccountId { get; set; }

			[JsonProperty("SubAccountId")]
			public long SubAccountId { get; set; }

			[JsonProperty("ClientOrderId")]
			public long ClientOrderId { get; set; }

			[JsonProperty("InstrumentId")]
			public long InstrumentId { get; set; }

			[JsonProperty("Side")]
			public string Side { get; set; }

			[JsonProperty("Quantity")]
			public long Quantity { get; set; }

			[JsonProperty("RemainingQuantity")]
			public long RemainingQuantity { get; set; }

			[JsonProperty("Price")]
			public long Price { get; set; }

			[JsonProperty("Value")]
			public long Value { get; set; }

			[JsonProperty("TradeTime")]
			public long TradeTime { get; set; }

			[JsonProperty("CounterParty")]
			public string CounterParty { get; set; }

			[JsonProperty("OrderTradeRevision")]
			public long OrderTradeRevision { get; set; }

			[JsonProperty("Direction")]
			public string Direction { get; set; }

			[JsonProperty("IsBlockTrade")]
			public bool IsBlockTrade { get; set; }

			public ExchangeTrade ToExchangeTrade()
			{
				var isBuy = Side.Equals("buy", StringComparison.InvariantCultureIgnoreCase);
				return new ExchangeTrade()
				{
					Amount = Quantity,
					Id = TradeId.ToStringInvariant(),
					Price = Price,
					IsBuy = isBuy,
					Timestamp = TradeTime.UnixTimeStampToDateTimeMilliseconds(),
					Flags = isBuy ? ExchangeTradeFlags.IsBuy : default
				};
			}
		}
    }
}
