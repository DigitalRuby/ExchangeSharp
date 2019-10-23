using ExchangeSharp.NDAX;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp.NDAX
{
	public enum Direction : byte
	{
		NoChange = 0,
		UpTick = 1,
		DownTick = 2,
	}
	public enum TakerSide : byte
	{
		Buy = 0,
		Sell = 1,
	}

	public class NDAXTrade : ExchangeTrade
	{
		public long Order1Id { get; set; }
		public long Order2Id { get; set; }
		public Direction Direction { get; set; }
		public bool IsBlockTrade { get; set; }
		public long ClientOrderId { get; set; }
		public override string ToString()
		{
			return string.Format("{0},{1},{2},{3},{4},{5}", base.ToString(),
				Order1Id, Order2Id, Direction, IsBlockTrade, ClientOrderId);
		}
	}
}

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		/// <summary>
		/// unable to use this in SubscribeTrades OnGetTradesWebSocketAsync() becuase of the array structure
		/// </summary>
		[JsonArray]
		class TradeData
		{
			[JsonProperty(Order = 0)]
			public long TradeId { get; set; }

			/// <summary>
			/// ProductPairCode is the same number and used for the same purpose as InstrumentID.
			/// The two are completely equivalent in value. InstrumentId 47 = ProductPairCode 47.
			/// </summary>
			[JsonProperty(Order = 1)]
			public long ProductPairCode { get; set; }

			[JsonProperty(Order = 2)]
			public long Quantity { get; set; }

			[JsonProperty(Order = 3)]
			public long Price { get; set; }

			[JsonProperty(Order = 4)]
			public long Order1Id { get; set; }

			[JsonProperty(Order = 5)]
			public long Order2Id { get; set; }

			[JsonProperty(Order = 6)]
			public long TradeTime { get; set; }

			[JsonProperty(Order = 7)]
			public Direction Direction { get; set; }

			[JsonProperty(Order = 8)]
			public TakerSide TakerSide { get; set; }

			[JsonProperty(Order = 9)]
			public bool IsBlockTrade { get; set; }

			[JsonProperty(Order = 10)]
			public long ClientOrderId { get; set; }

			public NDAXTrade ToExchangeTrade()
			{
				var isBuy = TakerSide == TakerSide.Buy;
				return new NDAXTrade()
				{
					Amount = Quantity,
					Id = TradeId.ToStringInvariant(),
					Price = Price,
					IsBuy = isBuy,
					Timestamp = TradeTime.UnixTimeStampToDateTimeMilliseconds(),
					Flags = isBuy ? ExchangeTradeFlags.IsBuy : default,
					Order1Id = Order1Id,
					Order2Id = Order2Id,
					Direction = Direction,
					IsBlockTrade = IsBlockTrade,
					ClientOrderId = ClientOrderId,
				};
			}
		}
	}
}
