using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/**
 [
  {
    "coin": "AGLD",
    "depositAllEnable": true,
    "withdrawAllEnable": true,
    "name": "Adventure Gold",
    "free": "0",
    "locked": "0",
    "freeze": "0",
    "withdrawing": "0",
    "ipoing": "0",
    "ipoable": "0",
    "storage": "0",
    "isLegalMoney": false,
    "trading": true,
    "networkList": [
      {
        "network": "ETH",
        "coin": "AGLD",
        "withdrawIntegerMultiple": "0.00000001",
        "isDefault": true,
        "depositEnable": true,
        "withdrawEnable": true,
        "depositDesc": "",
        "withdrawDesc": "",
        "specialTips": "",
        "specialWithdrawTips": "",
        "name": "Ethereum (ERC20)",
        "resetAddressStatus": false,
        "addressRegex": "^(0x)[0-9A-Fa-f]{40}$",
        "memoRegex": "",
        "withdrawFee": "4.67",
        "withdrawMin": "9.34",
        "withdrawMax": "9999999",
        "depositDust": "0.0012",
        "minConfirm": 6,
        "unLockConfirm": 64,
        "sameAddress": false,
        "estimatedArrivalTime": 5,
        "busy": false,
        "contractAddressUrl": "https://etherscan.io/address/",
        "contractAddress": "0x32353a6c91143bfd6c7d363b546e62a9a2489a20"
      }
    ]
  },
	...
]
*/

namespace ExchangeSharp.BinanceGroup
{
	public class Currency
	{
		[JsonProperty("coin")]
		public string Coin { get; set; }

		[JsonProperty("depositAllEnable")]
		public bool? DepositAllEnable { get; set; }

		[JsonProperty("withdrawAllEnable")]
		public bool? WithdrawAllEnable { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("free")]
		public decimal? Free { get; set; }

		[JsonProperty("locked")]
		public decimal? Locked { get; set; }

		[JsonProperty("freeze")]
		public decimal? Freeze { get; set; }

		[JsonProperty("withdrawing")]
		public decimal? Withdrawing { get; set; }

		[JsonProperty("ipoing")]
		public decimal? Ipoing { get; set; }

		[JsonProperty("ipoable")]
		public decimal? Ipoable { get; set; }

		[JsonProperty("storage")]
		public decimal? Storage { get; set; }

		[JsonProperty("isLegalMoney")]
		public bool IsLegalMoney { get; set; }

		[JsonProperty("trading")]
		public bool Trading { get; set; }

		[JsonProperty("networkList")]
		public List<Network> NetworkList { get; set; }
	}

	public class Network
	{
		[JsonProperty("network")]
		public string NetworkName { get; set; }

		[JsonProperty("coin")]
		public string Coin { get; set; }

		[JsonProperty("withdrawIntegerMultiple")]
		public decimal? WithdrawIntegerMultiple { get; set; }

		[JsonProperty("isDefault")]
		public bool IsDefault { get; set; }

		[JsonProperty("depositEnable")]
		public bool DepositEnable { get; set; }

		[JsonProperty("withdrawEnable")]
		public bool WithdrawEnable { get; set; }

		[JsonProperty("depositDesc")]
		public string DepositDesc { get; set; }

		[JsonProperty("withdrawDesc")]
		public string WithdrawDesc { get; set; }

		[JsonProperty("specialTips")]
		public string SpecialTips { get; set; }

		[JsonProperty("specialWithdrawTips")]
		public string SpecialWithdrawTips { get; set; }

		[JsonProperty("name")]
		public string NetworkDisplayName { get; set; }

		[JsonProperty("resetAddressStatus")]
		public bool? ResetAddressStatus { get; set; }

		[JsonProperty("addressRegex")]
		public string AddressRegex { get; set; }

		[JsonProperty("memoRegex")]
		public string MemoRegex { get; set; }

		[JsonProperty("withdrawFee")]
		public decimal? WithdrawFee { get; set; }

		[JsonProperty("withdrawMin")]
		public decimal? WithdrawMin { get; set; }

		[JsonProperty("withdrawMax")]
		public decimal? WithdrawMax { get; set; }

		[JsonProperty("depositDust")]
		public decimal? DepositDust { get; set; }

		[JsonProperty("minConfirm")]
		public int? MinConfirm { get; set; }

		[JsonProperty("unLockConfirm")]
		public int? UnLockConfirm { get; set; }

		[JsonProperty("sameAddress")]
		public bool? SameAddress { get; set; }

		[JsonProperty("estimatedArrivalTime")]
		public int? EstimatedArrivalTime { get; set; }

		[JsonProperty("busy")]
		public bool? Busy { get; set; }

		[JsonProperty("contractAddressUrl")]
		public string ContractAddressUrl { get; set; }

		[JsonProperty("contractAddress")]
		public string ContractAddress { get; set; }
	}
}
