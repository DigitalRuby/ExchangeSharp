using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models
{
	// ReSharper disable once InconsistentNaming
	public class BL3POrderRequest
	{
		[JsonProperty("price_int")]
		[JsonConverter(typeof(FixedIntDecimalJsonConverter), 5)]
		public decimal Price { get; set; }


		[JsonProperty("amount_int")]
		[JsonConverter(typeof(FixedIntDecimalJsonConverter), 8)]
		public decimal Amount { get; set; }

		public ExchangeOrderPrice ToExchangeOrder()
		{
			return new ExchangeOrderPrice
			{
				Amount = Amount,
				Price = Price
			};
		}
	}
}
