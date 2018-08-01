/*
MIT LICENSE

Copyright 2018 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp.Binance
{
    public class Currency
    {
        public string id { get; set; }

        public string assetCode { get; set; }

        public string assetName { get; set; }

        public string unit { get; set; }

        public decimal transactionFee { get; set; }

        public int commissionRate { get; set; }

        public int freeAuditWithdrawAmt { get; set; }

        public long freeUserChargeAmount { get; set; }

        public string minProductWithdraw { get; set; }

        public string withdrawIntegerMultiple { get; set; }

        public string confirmTimes { get; set; }

        public string chargeLockConfirmTimes { get; set; }

        public string url { get; set; }

        public string addressUrl { get; set; }

        public string blockUrl { get; set; }

        public bool enableCharge { get; set; }

        public bool enableWithdraw { get; set; }

        public string regEx { get; set; }

        public string regExTag { get; set; }

        public int gas { get; set; }

        public string parentCode { get; set; }

        public bool isLegalMoney { get; set; }

        public int reconciliationAmount { get; set; }

        public string seqNum { get; set; }

        public string chineseName { get; set; }

        public string cnLink { get; set; }

        public string enLink { get; set; }

        public string logoUrl { get; set; }

        public string fullLogoUrl { get; set; }

        public bool forceStatus { get; set; }

        public bool resetAddressStatus { get; set; }

        public object chargeDescCn { get; set; }

        public object chargeDescEn { get; set; }

        public object assetLabel { get; set; }

        public bool sameAddress { get; set; }

        public bool depositTipStatus { get; set; }

        public bool dynamicFeeStatus { get; set; }

        public object depositTipEn { get; set; }

        public object depositTipCn { get; set; }

        public object assetLabelEn { get; set; }

        public object supportMarket { get; set; }

        public string feeReferenceAsset { get; set; }

        public decimal? feeRate { get; set; }

        public int? feeDigit { get; set; }

        public bool legalMoney { get; set; }
    }
}