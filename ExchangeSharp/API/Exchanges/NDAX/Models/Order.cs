using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class Order
		{
			[JsonProperty("Side")]
			public string Side { get; set; }

			[JsonProperty("OrderId")]
			public long OrderId { get; set; }

			[JsonProperty("Price")]
			public decimal Price { get; set; }

			[JsonProperty("Quantity")]
			public decimal Quantity { get; set; }

			[JsonProperty("DisplayQuantity")]
			public long DisplayQuantity { get; set; }

			[JsonProperty("Instrument")]
			public int Instrument { get; set; }

			[JsonProperty("Account")]
			public long Account { get; set; }

			[JsonProperty("OrderType")]
			public string OrderType { get; set; }

			[JsonProperty("ClientOrderId")]
			public long ClientOrderId { get; set; }

			[JsonProperty("OrderState")]
			public string OrderState { get; set; }

			[JsonProperty("ReceiveTime")]
			public long ReceiveTime { get; set; }

			[JsonProperty("ReceiveTimeTicks")]
			public double ReceiveTimeTicks { get; set; }

			[JsonProperty("OrigQuantity")]
			public long OrigQuantity { get; set; }

			[JsonProperty("QuantityExecuted")]
			public long QuantityExecuted { get; set; }

			[JsonProperty("AvgPrice")]
			public long AvgPrice { get; set; }

			[JsonProperty("CounterPartyId")]
			public long CounterPartyId { get; set; }

			[JsonProperty("ChangeReason")]
			public string ChangeReason { get; set; }

			[JsonProperty("OrigOrderId")]
			public long OrigOrderId { get; set; }

			[JsonProperty("OrigClOrdId")]
			public long OrigClOrdId { get; set; }

			[JsonProperty("EnteredBy")]
			public long EnteredBy { get; set; }

			[JsonProperty("IsQuote")]
			public bool IsQuote { get; set; }

			[JsonProperty("InsideAsk")]
			public double InsideAsk { get; set; }

			[JsonProperty("InsideAskSize")]
			public long InsideAskSize { get; set; }

			[JsonProperty("InsideBid")]
			public long InsideBid { get; set; }

			[JsonProperty("InsideBidSize")]
			public long InsideBidSize { get; set; }

			[JsonProperty("LastTradePrice")]
			public long LastTradePrice { get; set; }

			[JsonProperty("RejectReason")]
			public string RejectReason { get; set; }

			[JsonProperty("IsLockedIn")]
			public bool IsLockedIn { get; set; }

			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			public ExchangeOrderResult ToExchangeOrderResult(Dictionary<string, long> symbolToIdMapping)
			{
				ExchangeAPIOrderResult orderResult;
				switch (OrderState.ToLowerInvariant())
				{
					case "working":
						orderResult = ExchangeAPIOrderResult.Pending;
						break;
					case "rejected":
						orderResult = ExchangeAPIOrderResult.Error;
						break;
					case "canceled":
						orderResult = ExchangeAPIOrderResult.Canceled;
						break;
					case "expired":
						orderResult = ExchangeAPIOrderResult.Canceled;
						break;
					case "fullyexecuted":
						orderResult = ExchangeAPIOrderResult.Filled;
						break;
					default:
						orderResult = ExchangeAPIOrderResult.Unknown;
						break;
				};
				var symbol = symbolToIdMapping.Where(pair => pair.Value.Equals(Instrument));
				return new ExchangeOrderResult()
				{
					Amount = Quantity,
					IsBuy = Side.Equals("buy", StringComparison.InvariantCultureIgnoreCase),
					MarketSymbol = symbol.Any() ? symbol.First().Key : null,
					Price = Price,
					Result = orderResult,
					OrderDate = ReceiveTime.UnixTimeStampToDateTimeMilliseconds(),

					OrderId = OrderId.ToStringInvariant(),

				};
			}
		}
    }
}
