using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class AccountBalance
		{
			[JsonProperty("OMSId")]
			public long OmsId { get; set; }

			[JsonProperty("AccountId")]
			public long AccountId { get; set; }

			[JsonProperty("ProductSymbol")]
			public string ProductSymbol { get; set; }

			[JsonProperty("ProductId")]
			public long ProductId { get; set; }

			[JsonProperty("Amount")]
			public decimal Amount { get; set; }

			[JsonProperty("Hold")]
			public long Hold { get; set; }

			[JsonProperty("PendingDeposits")]
			public long PendingDeposits { get; set; }

			[JsonProperty("PendingWithdraws")]
			public long PendingWithdraws { get; set; }

			[JsonProperty("TotalDayDeposits")]
			public long TotalDayDeposits { get; set; }

			[JsonProperty("TotalDayWithdraws")]
			public long TotalDayWithdraws { get; set; }

			[JsonProperty("TotalMonthWithdraws")]
			public long TotalMonthWithdraws { get; set; }
		}
	}
}
