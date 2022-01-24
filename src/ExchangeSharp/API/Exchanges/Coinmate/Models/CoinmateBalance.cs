namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateBalance
	{
		public string Currency { get; set; }
		public decimal Balance { get; set; }
		public decimal Reserved { get; set; }
		public decimal Available { get; set; }
	}
}
