/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
namespace ExchangeSharp.BinanceGroup
{
    using Newtonsoft.Json;

    internal class Currency
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("assetCode")]
        public string? AssetCode { get; set; }

        [JsonProperty("assetName")]
        public string? AssetName { get; set; }

        [JsonProperty("unit")]
        public string? Unit { get; set; }

        [JsonProperty("transactionFee")]
        public decimal TransactionFee { get; set; }

        [JsonProperty("commissionRate")]
        public decimal CommissionRate { get; set; }

        [JsonProperty("freeAuditWithdrawAmt")]
        public decimal FreeAuditWithdrawAmt { get; set; }

        [JsonProperty("freeUserChargeAmount")]
        public long FreeUserChargeAmount { get; set; }

        [JsonProperty("minProductWithdraw")]
        public string? MinProductWithdraw { get; set; }

        [JsonProperty("withdrawIntegerMultiple")]
        public string? WithdrawIntegerMultiple { get; set; }

        [JsonProperty("confirmTimes")]
        public string? ConfirmTimes { get; set; }

        [JsonProperty("chargeLockConfirmTimes")]
        public string? ChargeLockConfirmTimes { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("addressUrl")]
        public string? AddressUrl { get; set; }

        [JsonProperty("blockUrl")]
        public string? BlockUrl { get; set; }

        [JsonProperty("enableCharge")]
        public bool EnableCharge { get; set; }

        [JsonProperty("enableWithdraw")]
        public bool EnableWithdraw { get; set; }

        [JsonProperty("regEx")]
        public string? RegEx { get; set; }

        [JsonProperty("regExTag")]
        public string? RegExTag { get; set; }

        [JsonProperty("gas")]
        public decimal Gas { get; set; }

        [JsonProperty("parentCode")]
        public string? ParentCode { get; set; }

        [JsonProperty("isLegalMoney")]
        public bool IsLegalMoney { get; set; }

        [JsonProperty("reconciliationAmount")]
        public decimal ReconciliationAmount { get; set; }

        [JsonProperty("seqNum")]
        public string? SeqNum { get; set; }

        [JsonProperty("chineseName")]
        public string? ChineseName { get; set; }

        [JsonProperty("cnLink")]
        public string? CnLink { get; set; }

        [JsonProperty("enLink")]
        public string? EnLink { get; set; }

        [JsonProperty("logoUrl")]
        public string? LogoUrl { get; set; }

        [JsonProperty("fullLogoUrl")]
        public string? FullLogoUrl { get; set; }

        [JsonProperty("forceStatus")]
        public bool ForceStatus { get; set; }

        [JsonProperty("resetAddressStatus")]
        public bool ResetAddressStatus { get; set; }

        [JsonProperty("chargeDescCn")]
        public object? ChargeDescCn { get; set; }

        [JsonProperty("chargeDescEn")]
        public object? ChargeDescEn { get; set; }

        [JsonProperty("assetLabel")]
        public object? AssetLabel { get; set; }

        [JsonProperty("sameAddress")]
        public bool SameAddress { get; set; }

        [JsonProperty("depositTipStatus")]
        public bool DepositTipStatus { get; set; }

        [JsonProperty("dynamicFeeStatus")]
        public bool DynamicFeeStatus { get; set; }

        [JsonProperty("depositTipEn")]
        public object? DepositTipEn { get; set; }

        [JsonProperty("depositTipCn")]
        public object? DepositTipCn { get; set; }

        [JsonProperty("assetLabelEn")]
        public object? AssetLabelEn { get; set; }

        [JsonProperty("supportMarket")]
        public object? SupportMarket { get; set; }

        [JsonProperty("feeReferenceAsset")]
        public string? FeeReferenceAsset { get; set; }

        [JsonProperty("feeRate")]
        public decimal? FeeRate { get; set; }

        [JsonProperty("feeDigit")]
        public int? FeeDigit { get; set; }

        [JsonProperty("legalMoney")]
        public bool LegalMoney { get; set; }
    }
}
