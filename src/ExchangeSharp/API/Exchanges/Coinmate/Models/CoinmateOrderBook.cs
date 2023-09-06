namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateOrderBook
	{
		public AskBid[] Asks { get; set; }
		public AskBid[] Bids { get; set; }

		public class AskBid
		{
			public decimal Price { get; set; }
			public decimal Amount { get; set; }
		}
	}
}
