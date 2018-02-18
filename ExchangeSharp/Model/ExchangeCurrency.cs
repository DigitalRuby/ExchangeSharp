﻿namespace ExchangeSharp
{
    public class ExchangeCurrency
    {
        /// <summary>Short name of the currency. Eg. ETH</summary>
        public string Name { get; set; }

        /// <summary>Full name of the currency. Eg. Ethereum</summary>
        public string FullName { get; set; }

        public decimal TxFee { get; set; }

        /// <summary>A value indicating whether the currency is enabled for deposits/withdrawals.</summary>
        public bool IsEnabled { get; set; }
    }
}