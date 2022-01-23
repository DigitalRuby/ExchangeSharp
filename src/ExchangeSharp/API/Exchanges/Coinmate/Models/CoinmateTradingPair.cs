namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateTradingPair
	{
		public string Name { get; set; }
		public string FirstCurrency { get; set; }
		public string SecondCurrency { get; set; }
		public int PriceDecimals { get; set; }
		public int LotDecimals { get; set; }
		public decimal MinAmount { get; set; }
		public string TradesWebSocketChannelId { get; set; }
		public string OrderBookWebSocketChannelId { get; set; }
		public string TradeStatisticsWebSocketChannelId { get; set; }
	}
}
