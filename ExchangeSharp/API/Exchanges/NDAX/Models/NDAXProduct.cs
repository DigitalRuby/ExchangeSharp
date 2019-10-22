using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class NDAXProduct
		{
			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			[JsonProperty("ProductId")]
			public long ProductId { get; set; }

			[JsonProperty("Product")]
			public string Product { get; set; }

			[JsonProperty("ProductFullName")]
			public string ProductFullName { get; set; }

			[JsonProperty("ProductType")]
			public string ProductType { get; set; }

			[JsonProperty("DecimalPlaces")]
			public long DecimalPlaces { get; set; }

			[JsonProperty("TickSize")]
			public long TickSize { get; set; }

			[JsonProperty("NoFees")]
			public bool NoFees { get; set; }

			public ExchangeCurrency ToExchangeCurrency()
			{
				return new ExchangeCurrency()
				{
					Name = Product,
					FullName = ProductFullName
				};
			}
		}
    }
}
