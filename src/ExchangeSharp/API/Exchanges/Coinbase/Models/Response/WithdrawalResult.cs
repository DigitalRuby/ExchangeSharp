using Newtonsoft.Json;

namespace ExchangeSharp.Coinbase
{
	public class WithdrawalResult
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("amount")]
		public string Amount { get; set; }

		[JsonProperty("currency")]
		public string Currency { get; set; }

		[JsonProperty("fee")]
		public string Fee { get; set; }

		[JsonProperty("subtotal")]
		public string Subtotal { get; set; }
	}
}
