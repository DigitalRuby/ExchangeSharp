using Newtonsoft.Json;

namespace ExchangeSharp.BinanceGroup
{
	public class CurrencyNetwork
	{
		[JsonProperty("addressRegex")]
		public string AddressRegex { get; set; }

		[JsonProperty("coin")]
		public string Coin { get; set; }

		[JsonProperty("depositDesc")]
		public string DepositDesc { get; set; }

		[JsonProperty("depositEnable")]
		public bool DepositEnable { get; set; }

		[JsonProperty("isDefault")]
		public bool IsDefault { get; set; }

		[JsonProperty("memoRegex")]
		public string MemoRegex { get; set; }

		[JsonProperty("minConfirm")]
		public int MinConfirm { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("network")]
		public string Network { get; set; }

		[JsonProperty("resetAddressStatus")]
		public bool ResetAddressStatus { get; set; }

		[JsonProperty("specialTips")]
		public string SpecialTips { get; set; }

		[JsonProperty("unLockConfirm")]
		public int UnLockConfirm { get; set; }

		[JsonProperty("withdrawDesc")]
		public string WithdrawDesc { get; set; }

		[JsonProperty("withdrawEnable")]
		public bool WithdrawEnable { get; set; }

		[JsonProperty("withdrawFee")]
		public string WithdrawFee { get; set; }

		[JsonProperty("withdrawIntegerMultiple")]
		public string WithdrawIntegerMultiple { get; set; }

		[JsonProperty("withdrawMax")]
		public string WithdrawMax { get; set; }

		[JsonProperty("withdrawMin")]
		public string WithdrawMin { get; set; }

		[JsonProperty("sameAddress")]
		public bool SameAddress { get; set; }
	}
}
