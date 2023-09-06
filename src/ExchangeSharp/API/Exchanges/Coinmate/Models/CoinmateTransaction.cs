namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateTransaction
	{
		public long Timestamp { get; set; }
		public string TransactionId { get; set; }
		public decimal Price { get; set; }
		public decimal Amount { get; set; }
		public string CurrencyPair { get; set; }
		public string TradeType { get; set; }
	}
}
