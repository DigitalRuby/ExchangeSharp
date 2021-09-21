using Newtonsoft.Json;

namespace ExchangeSharp.BinanceGroup
{
	public class Currency
	{
		[JsonProperty("coin")]
		public string Coin { get; set; }

		[JsonProperty("depositAllEnable")]
		public bool DepositAllEnable { get; set; }

		[JsonProperty("free")]
		public string Free { get; set; }

		[JsonProperty("freeze")]
		public string Freeze { get; set; }

		[JsonProperty("ipoable")]
		public string Ipoable { get; set; }

		[JsonProperty("ipoing")]
		public string Ipoing { get; set; }

		[JsonProperty("isLegalMoney")]
		public bool IsLegalMoney { get; set; }

		[JsonProperty("locked")]
		public string Locked { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("storage")]
		public string Storage { get; set; }

		[JsonProperty("trading")]
		public bool Trading { get; set; }

		[JsonProperty("withdrawAllEnable")]
		public bool WithdrawAllEnable { get; set; }

		[JsonProperty("withdrawing")]
		public string Withdrawing { get; set; }

		[JsonProperty("networkList")]
		public CurrencyNetwork[] NetworkList { get; set; }
	}
}
