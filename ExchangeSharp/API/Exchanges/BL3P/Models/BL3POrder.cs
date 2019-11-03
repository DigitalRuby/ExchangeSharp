using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeBL3PAPI : ExchangeAPI
	{
		// ReSharper disable once InconsistentNaming
		class BL3POrder
		{
			[JsonProperty("price_int")]
			[JsonConverter(typeof(FixedIntDecimalConverter), 5)]
			public decimal Price { get; set; }


			[JsonProperty("amount_int")]
			[JsonConverter(typeof(FixedIntDecimalConverter), 8)]
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
}
