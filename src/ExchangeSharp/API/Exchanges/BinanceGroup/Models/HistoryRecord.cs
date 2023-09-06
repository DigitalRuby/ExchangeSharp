using Newtonsoft.Json;

namespace ExchangeSharp.BinanceGroup
{
	public class HistoryRecord
	{
		[JsonProperty("amount")]
		public string Amount { get; set; }

		[JsonProperty("coin")]
		public string Coin { get; set; }

		[JsonProperty("network")]
		public string Network { get; set; }

		[JsonProperty("status")]
		public int Status { get; set; }

		[JsonProperty("address")]
		public string Address { get; set; }

		[JsonProperty("addressTag")]
		public string AddressTag { get; set; }

		[JsonProperty("txId")]
		public string TxId { get; set; }

		[JsonProperty("insertTime")]
		public long InsertTime { get; set; }

		[JsonProperty("transferType")]
		public int TransferType { get; set; }

		[JsonProperty("unlockConfirm")]
		public string UnlockConfirm { get; set; }

		[JsonProperty("confirmTimes")]
		public string ConfirmTimes { get; set; }
	}
}
