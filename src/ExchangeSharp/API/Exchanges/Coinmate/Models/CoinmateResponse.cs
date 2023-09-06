namespace ExchangeSharp.API.Exchanges.Coinmate.Models
{
	public class CoinmateResponse<T>
	{
		public bool Error { get; set; }
		public string ErrorMessage { get; set; }
		public T Data { get; set; }
	}
}
