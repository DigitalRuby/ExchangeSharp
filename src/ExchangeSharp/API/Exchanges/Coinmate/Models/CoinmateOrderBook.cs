namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateOrderBook
	{
		public AskBid[] Asks { get; set; }
		public AskBid[] Bids { get; set; }
	
		public class AskBid
		{
			public decimal price { get; set; }
			public decimal amount { get; set; }
		}
	}
}
