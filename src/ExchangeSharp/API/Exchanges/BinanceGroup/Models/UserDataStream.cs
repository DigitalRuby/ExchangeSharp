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
		public decimal CommissionAmount { get; set; }

		[JsonProperty("N")]
		public string CommissionAsset { get; set; }

		[JsonProperty("T")]
		public long TransactionTime { get; set; }

		[JsonProperty("t")]
		public string TradeId { get; set; }

		[JsonProperty("w")]
		public string IsTheOrderWorking { get; set; }

		[JsonProperty("m")]
		public string IsThisTradeTheMakerSide { get; set; }

		[JsonProperty("O")]
		public long OrderCreationTime { get; set; }

		[JsonProperty("Z")]
		public decimal CumulativeQuoteAssetTransactedQuantity { get; set; }

		[JsonProperty("Y")]
		public decimal LastQuoteAssetTransactedQuantity { get; set; }

		public override string ToString()
		{
			return $"{nameof(Symbol)}: {Symbol}, {nameof(OrderType)}: {OrderType}, {nameof(OrderQuantity)}: {OrderQuantity}, {nameof(OrderPrice)}: {OrderPrice}, {nameof(CurrentOrderStatus)}: {CurrentOrderStatus}, {nameof(OrderId)}: {OrderId}";
		}

		/// <summary>
		/// convert current instance to ExchangeOrderResult
		/// </summary>
		public ExchangeOrderResult ExchangeOrderResult
		{
			get
			{
				var status = BinanceGroupCommon.ParseExchangeAPIOrderResult(
						status: CurrentOrderStatus,
						amountFilled: CumulativeFilledQuantity
				);
				return new ExchangeOrderResult()
				{
					OrderId = OrderId.ToString(),
					ClientOrderId = ClientOrderId,
					Result = status,
					ResultCode = CurrentOrderStatus,
					Message = OrderRejectReason, // can use for multiple things in the future if needed
					AmountFilled =
								TradeId != null ? LastExecutedQuantity : CumulativeFilledQuantity,
					Price = OrderPrice,
					AveragePrice =
								CumulativeQuoteAssetTransactedQuantity / CumulativeFilledQuantity, // Average price can be found by doing Z divided by z.
					OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
								OrderCreationTime
						),
					CompletedDate = status.IsCompleted()
								? (DateTime?)
										CryptoUtility.UnixTimeStampToDateTimeMilliseconds(TransactionTime)
								: null,
					TradeDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(TransactionTime),
					UpdateSequence = EventTime, // in Binance, the sequence nymber is also the EventTime
					MarketSymbol = Symbol,
					// IsBuy is not provided here
					Fees = CommissionAmount,
					FeesCurrency = CommissionAsset,
					TradeId = TradeId,
				};
			}
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

	/// <summary>
	/// For Binance User Data stream (different from Balance): Balance Update occurs during the following:
	/// - Deposits or withdrawals from the account
	/// - Transfer of funds between accounts(e.g.Spot to Margin)
	/// </summary>
	internal class BalanceUpdate
	{
		[JsonProperty("e")]
		public string EventType { get; set; }

		[JsonProperty("E")]
		public long EventTime { get; set; }

		[JsonProperty("a")]
		public string Asset { get; set; }

		[JsonProperty("d")]
		public decimal BalanceDelta { get; set; }

		[JsonProperty("T")]
		public long ClearTime { get; set; }
	}

	/// <summary>
	/// As part of outboundAccountPosition from Binance User Data Stream (different from BalanceUpdate)
	/// </summary>
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

	/// <summary>
	/// outboundAccountPosition is sent any time an account balance has changed and contains
	/// the assets that were possibly changed by the event that generated the balance change.
	/// </summary>
	internal class OutboundAccount
	{
		[JsonProperty("e")]
		public string EventType { get; set; }

		[JsonProperty("E")]
		public long EventTime { get; set; }

		[JsonProperty("u")]
		public long LastAccountUpdate { get; set; }

		[JsonProperty("B")]
		public List<Balance> Balances { get; set; }

		/// <summary> convert the Balances list to a dictionary of total amounts </summary>
		public Dictionary<string, decimal> BalancesAsTotalDictionary
		{
			get
			{
				var dict = new Dictionary<string, decimal>();
				foreach (var balance in Balances)
				{
					dict.Add(balance.Asset, balance.Free + balance.Locked);
				}
				return dict;
			}
		}

		/// <summary> convert the Balances list to a dictionary of available to trade amounts </summary>
		public Dictionary<string, decimal> BalancesAsAvailableToTradeDictionary
		{
			get
			{
				var dict = new Dictionary<string, decimal>();
				foreach (var balance in Balances)
				{
					dict.Add(balance.Asset, balance.Free);
				}
				return dict;
			}
		}
	}
}
