namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateOrder
	{
		public int Id { get; set; }
		public long Timestamp { get; set; }
		public string Type { get; set; }
		public decimal? Price { get; set; }
		public decimal? RemainingAmount { get; set; }
		public decimal OriginalAmount { get; set; }
		public decimal? StopPrice { get; set; }
		public string Status { get; set; }
		public string OrderTradeType { get; set; }
		public decimal? AvgPrice { get; set; }
		public bool Trailing { get; set; }
		public string StopLossOrderId { get; set; }
		public string OriginalOrderId { get; set; }
	}
}
