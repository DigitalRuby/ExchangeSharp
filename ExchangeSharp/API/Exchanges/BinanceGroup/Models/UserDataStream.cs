using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ExchangeSharp.BinanceGroup
{
	internal class ExecutionReport
	{
		[JsonProperty("e")]
		public string EventType { get; set; }
		[JsonProperty("E")]
		public long EventTime { get; set; }
		[JsonProperty("s")]
		public string Symbol { get; set; }
		[JsonProperty("c")]
		public string ClientOrderId { get; set; }
		[JsonProperty("S")]
		public string Side { get; set; }
		[JsonProperty("o")]
		public string OrderType { get; set; }
		[JsonProperty("f")]
		public string TimeInForce { get; set; }
		[JsonProperty("q")]
		public decimal OrderQuantity { get; set; }
		[JsonProperty("p")]
		public decimal OrderPrice { get; set; }
		[JsonProperty("P")]
		public decimal StopPrice { get; set; }
		[JsonProperty("F")]
		public decimal IcebergQuantity { get; set; }
		[JsonProperty("g")]
		public int OrderListId { get; set; }
		[JsonProperty("C")]
		public string OriginalClientOrderId { get; set; }
		[JsonProperty("x")]
		public string CurrentExecutionType { get; set; }
		[JsonProperty("X")]
		public string CurrentOrderStatus { get; set; }
		[JsonProperty("r")]
		public string OrderRejectReason { get; set; }
		[JsonProperty("i")]
		public int OrderId { get; set; }
		[JsonProperty("l")]
		public decimal LastExecutedQuantity { get; set; }
		[JsonProperty("z")]
		public decimal CumulativeFilledQuantity { get; set; }
		[JsonProperty("L")]
		public decimal LastExecutedPrice { get; set; }
		[JsonProperty("n")]
		public string CommissionAmount { get; set; }
		[JsonProperty("N")]
		public string CommissionAsset { get; set; }
		[JsonProperty("T")]
		public string TransactionTime { get; set; }
		[JsonProperty("t")]
		public string TradeId { get; set; }
		[JsonProperty("w")]
		public string IsTheOrderWorking { get; set; }
		[JsonProperty("m")]
		public string IsThisTradeTheMakerSide { get; set; }
		[JsonProperty("O")]
		public string OrderCreationTime { get; set; }
		[JsonProperty("Z")]
		public decimal CumulativeQuoteAssetTransactedQuantity { get; set; }
		[JsonProperty("Y")]
		public decimal LastQuoteAssetTransactedQuantity { get; set; }

		public override string ToString()
		{
			return $"{nameof(Symbol)}: {Symbol}, {nameof(OrderType)}: {OrderType}, {nameof(OrderQuantity)}: {OrderQuantity}, {nameof(OrderPrice)}: {OrderPrice}, {nameof(CurrentOrderStatus)}: {CurrentOrderStatus}, {nameof(OrderId)}: {OrderId}";
		}

	}

	internal class Order
	{
		[JsonProperty("s")]
		public string Symbol { get; set; }
		[JsonProperty("i")]
		public int OrderId { get; set; }
		[JsonProperty("c")]
		public string ClientOrderId { get; set; }

		public override string ToString()
		{
			return $"{nameof(Symbol)}: {Symbol}, {nameof(OrderId)}: {OrderId}, {nameof(ClientOrderId)}: {ClientOrderId}";
		}
	}

	internal class ListStatus
	{
		[JsonProperty("e")]
		public string EventType { get; set; }
		[JsonProperty("E")]
		public long EventTime { get; set; }
		[JsonProperty("s")]
		public string Symbol { get; set; }
		[JsonProperty("g")]
		public int OrderListId { get; set; }
		[JsonProperty("c")]
		public string ContingencyType { get; set; }
		[JsonProperty("l")]
		public string ListStatusType { get; set; }
		[JsonProperty("L")]
		public string ListOrderStatus { get; set; }
		[JsonProperty("r")]
		public string ListRejectReason { get; set; }
		[JsonProperty("C")]
		public string ListClientOrderId { get; set; }
		[JsonProperty("T")]
		public long TransactionTime { get; set; }
		[JsonProperty("O")]
		public List<Order> Orders { get; set; }

		public override string ToString()
		{
			return $"{nameof(EventType)}: {EventType}, {nameof(EventTime)}: {EventTime}, {nameof(Symbol)}: {Symbol}, {nameof(OrderListId)}: {OrderListId}, {nameof(ContingencyType)}: {ContingencyType}, {nameof(ListStatusType)}: {ListStatusType}, {nameof(ListOrderStatus)}: {ListOrderStatus}, {nameof(ListRejectReason)}: {ListRejectReason}, {nameof(ListClientOrderId)}: {ListClientOrderId}, {nameof(TransactionTime)}: {TransactionTime}, {nameof(Orders)}: {Orders}";
		}
	}

	internal class Balance
	{
		[JsonProperty("a")]
		public string Asset { get; set; }
		[JsonProperty("f")]
		public decimal Free { get; set; }
		[JsonProperty("l")]
		public decimal Locked { get; set; }

		public override string ToString()
		{
			return $"{nameof(Asset)}: {Asset}, {nameof(Free)}: {Free}, {nameof(Locked)}: {Locked}";
		}
	}

	internal class OutboundAccount
	{
		[JsonProperty("e")]
		public string EventType { get; set; }
		[JsonProperty("E")]
		public long EventTime { get; set; }
		[JsonProperty("m")]
		public int MakerCommissionRate { get; set; }
		[JsonProperty("t")]
		public int TakerCommissionRate { get; set; }
		[JsonProperty("b")]
		public int BuyerCommissionRate { get; set; }
		[JsonProperty("s")]
		public int SellerCommissionRate { get; set; }
		[JsonProperty("T")]
		public bool CanTrade { get; set; }
		[JsonProperty("W")]
		public bool CanWithdraw { get; set; }
		[JsonProperty("D")]
		public bool CanDeposit { get; set; }
		[JsonProperty("u")]
		public long LastAccountUpdate { get; set; }
		[JsonProperty("B")]
		public List<Balance> Balances { get; set; }
	}
}
