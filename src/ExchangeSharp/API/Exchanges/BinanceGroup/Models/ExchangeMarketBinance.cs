using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.BinanceGroup
{
	/// <summary>
	/// Binance SymbolStatus
	/// </summary>
	public enum BinanceSymbolStatus : byte
	{
		/// <summary>
		/// Pre-trading.
		/// </summary>
		PreTrading = 2,

		/// <summary>
		/// Trading.
		/// </summary>
		Trading = 4,

		/// <summary>
		/// Post-trading
		/// </summary>
		PostTrading = 6,

		/// <summary>
		/// End-of-day
		/// </summary>
		EndOfDay = 8,

		/// <summary>
		/// Halt.
		/// </summary>
		Halt = 10,

		/// <summary>
		/// Auction match.
		/// </summary>
		AuctionMatch = 12,

		/// <summary>
		/// Break.
		/// </summary>
		Break = 14,
	}

	public sealed class ExchangeMarketBinance : ExchangeMarket
	{
		public BinanceSymbolStatus Status { get; set; }
		public int BaseAssetPrecision { get; set; }
		public int QuotePrecision { get; set; }
		public int MaxNumOrders { get; set; }
		public bool IsIceBergAllowed { get; set; }
	}
}
