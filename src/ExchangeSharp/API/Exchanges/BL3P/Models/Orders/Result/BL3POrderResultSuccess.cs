using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ExchangeSharp.BL3P
{
	internal class BL3POrderResultSuccess : BL3PResponsePayload
	{
		[JsonProperty("date")]
		[JsonConverter(typeof(UnixDateTimeConverter))]
		public DateTime Date { get; set; }

		/// <summary>
		/// The time the order got closed. (not available for market orders and cancelled orders)
		/// </summary>
		[JsonProperty("date_closed")]
		[JsonConverter(typeof(UnixDateTimeConverter))]
		public DateTime? DateClosed { get; set; }

		/// <summary>
		/// Total amount in EUR of the trades that got executed.
		/// </summary>

		[JsonProperty("total_spent")]
		public BL3PAmount TotalSpent { get; set; }

		/// <summary>
		/// Total order amount of BTC or LTC.
		/// </summary>
		[JsonProperty("amount")]
		public BL3PAmount Amount { get; set; }

		/// <summary>
		/// Amount of funds (usually EUR) used in this order
		/// </summary>
		[JsonProperty("amount_funds")]
		public BL3PAmount AmountFunds { get; set; }

		/// <summary>
		/// Total amount of the trades that got executed. (Can be: BTC or LTC).
		/// </summary>
		[JsonProperty("total_amount")]
		public BL3PAmount TotalAmount { get; set; }

		/// <summary>
		/// Order limit price.
		/// </summary>
		[JsonProperty("price")]
		public BL3PAmount Price { get; set; }

		/// <summary>
		/// Total fee incurred in BTC or LTC.
		/// </summary>
		[JsonProperty("total_fee")]
		public BL3PAmount TotalFee { get; set; }

		/// <summary>
		/// Average cost of executed trades.
		/// </summary>
		[JsonProperty("avg_cost")]
		public BL3PAmount? AverageCost { get; set; }

		/// <summary>
		/// The item that will be traded for `currency`. (Can be: 'BTC')
		/// </summary>
		[JsonProperty("item")]
		public string Item { get; set; }

		/// <summary>
		/// Array of trades executed for the this order.
		/// </summary>
		[JsonProperty("trades")]
		public BL3PAmount[] Trades { get; set; }

		/// <summary>
		/// API Key Label
		/// </summary>
		[JsonProperty("label")]
		public string APIKeyLabel { get; set; }

		/// <summary>
		/// Type of the order. Can be bid or ask
		/// </summary>
		[JsonProperty("type", Required = Required.Always)]
		public BL3POrderType Type { get; set; }

		/// <summary>
		/// Currency of the order. (Is now by default 'EUR')
		/// <see cref="Item"/>
		/// </summary>
		[JsonProperty("currency")]
		public string Currency { get; set; }

		/// <summary>
		/// Id of the order.
		/// </summary>
		[JsonProperty("order_id")]
		public string OrderId { get; set; }

		/// <summary>
		/// Trade ID
		/// </summary>
		[JsonProperty("trade_id")]
		public string? TradeId { get; set; }

		/// <summary>
		/// Order status
		/// </summary>
		[JsonProperty("status", Required = Required.Always)]
		public BL3POrderStatus Status { get; set; }
	}
}
