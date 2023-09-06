using System;
using Newtonsoft.Json;

namespace ExchangeSharp.BinanceGroup
{
	public class Currency
	{
		[JsonProperty("id")]
		public long? Id { get; set; }

		[JsonProperty("assetCode")]
		public string AssetCode { get; set; }

		[JsonProperty("assetName")]
		public string AssetName { get; set; }

		[JsonProperty("unit")]
		public string Unit { get; set; }

		[JsonProperty("transactionFee")]
		public double? TransactionFee { get; set; }

		[JsonProperty("commissionRate")]
		public long? CommissionRate { get; set; }

		[JsonProperty("freeAuditWithdrawAmt")]
		public long? FreeAuditWithdrawAmt { get; set; }

		[JsonProperty("freeUserChargeAmount")]
		public long? FreeUserChargeAmount { get; set; }

		[JsonProperty("minProductWithdraw")]
		public decimal? MinProductWithdraw { get; set; }

		[JsonProperty("withdrawIntegerMultiple")]
		public float? WithdrawIntegerMultiple { get; set; }

		[JsonProperty("confirmTimes")]
		public int? ConfirmTimes { get; set; }

		[JsonProperty("chargeLockConfirmTimes")]
		public int? ChargeLockConfirmTimes { get; set; }

		[JsonProperty("createTime")]
		public object CreateTime { get; set; }

		[JsonProperty("test")]
		public long? Test { get; set; }

		[JsonProperty("url")]
		public Uri Url { get; set; }

		[JsonProperty("addressUrl")]
		public Uri AddressUrl { get; set; }

		[JsonProperty("blockUrl")]
		public string BlockUrl { get; set; }

		[JsonProperty("enableCharge")]
		public bool? EnableCharge { get; set; }

		[JsonProperty("enableWithdraw")]
		public bool? EnableWithdraw { get; set; }

		[JsonProperty("regEx")]
		public string RegEx { get; set; }

		[JsonProperty("regExTag")]
		public string RegExTag { get; set; }

		[JsonProperty("gas")]
		public long? Gas { get; set; }

		[JsonProperty("parentCode")]
		public string ParentCode { get; set; }

		[JsonProperty("isLegalMoney")]
		public bool? IsLegalMoney { get; set; }

		[JsonProperty("reconciliationAmount")]
		public long? ReconciliationAmount { get; set; }

		[JsonProperty("seqNum")]
		public long? SeqNum { get; set; }

		[JsonProperty("chineseName")]
		public string ChineseName { get; set; }

		[JsonProperty("cnLink")]
		public Uri CnLink { get; set; }

		[JsonProperty("enLink")]
		public Uri EnLink { get; set; }

		[JsonProperty("logoUrl")]
		public string LogoUrl { get; set; }

		[JsonProperty("fullLogoUrl")]
		public Uri FullLogoUrl { get; set; }

		[JsonProperty("forceStatus")]
		public bool? ForceStatus { get; set; }

		[JsonProperty("resetAddressStatus")]
		public bool? ResetAddressStatus { get; set; }

		[JsonProperty("chargeDescCn")]
		public object ChargeDescCn { get; set; }

		[JsonProperty("chargeDescEn")]
		public object ChargeDescEn { get; set; }

		[JsonProperty("assetLabel")]
		public object AssetLabel { get; set; }

		[JsonProperty("sameAddress")]
		public bool? SameAddress { get; set; }

		[JsonProperty("depositTipStatus")]
		public bool? DepositTipStatus { get; set; }

		[JsonProperty("dynamicFeeStatus")]
		public bool? DynamicFeeStatus { get; set; }

		[JsonProperty("depositTipEn")]
		public object DepositTipEn { get; set; }

		[JsonProperty("depositTipCn")]
		public object DepositTipCn { get; set; }

		[JsonProperty("assetLabelEn")]
		public object AssetLabelEn { get; set; }

		[JsonProperty("supportMarket")]
		public object SupportMarket { get; set; }

		[JsonProperty("feeReferenceAsset")]
		public string FeeReferenceAsset { get; set; }

		[JsonProperty("feeRate")]
		public decimal? FeeRate { get; set; }

		[JsonProperty("feeDigit")]
		public long? FeeDigit { get; set; }

		[JsonProperty("legalMoney")]
		public bool? LegalMoney { get; set; }
	}
}
