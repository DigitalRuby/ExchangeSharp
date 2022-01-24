namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateOpenOrder
	{
		public int Id { get; set; }
		public long Timestamp { get; set; }
		public string Type { get; set; }
		public string CurrencyPair { get; set; }
		public decimal Price { get; set; }
		public decimal Amount { get; set; }
		public decimal? StopPrice { get; set; }
		public string OrderTradeType { get; set; }
		public bool Hidden { get; set; }
		public bool Trailing { get; set; }
		public long? StopLossOrderId { get; set; }
		public long? ClientOrderId { get; set; }
	}
}
