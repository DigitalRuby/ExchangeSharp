/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    public sealed class ExchangeCurrency
    {
        /// <summary>Short name of the currency. Eg. ETH</summary>
        public string Name { get; set; }

        /// <summary>Full name of the currency. Eg. Ethereum</summary>
        public string FullName { get; set; }

        /// <summary>Alternate name, null if none</summary>
        public string AltName { get; set; }

        /// <summary>The transaction fee.</summary>
        public decimal TxFee { get; set; }

        /// <summary>A value indicating whether deposit is enabled.</summary>
        public bool DepositEnabled { get; set; }

        /// <summary>A value indicating whether withdrawal is enabled.</summary>
        public bool WithdrawalEnabled { get; set; }

        /// <summary>
        /// The minimum size of a withdrawal request
        /// </summary>
        public decimal MinWithdrawalSize { get; set; }

        /// <summary>Extra information from the exchange.</summary>
        public string Notes { get; set; }

        /// <summary>
        /// The type of the coin.
        /// Examples from Bittrex: BITCOIN, NXT, BITCOINEX, NXT_MS,
        /// CRYPTO_NOTE_PAYMENTID, BITSHAREX, COUNTERPARTY, BITCOIN_STEALTH, RIPPLE, ETH_CONTRACT,
        /// NEM, ETH, OMNI, LUMEN, FACTOM, STEEM, BITCOIN_PERCENTAGE_FEE, LISK, WAVES, ANTSHARES,
        /// WAVES_ASSET, BYTEBALL, SIA, ADA
        /// </summary>
        public string CoinType { get; set; }

        /// <summary>The base address where a two-field coin is sent. For example, Ripple deposits
        /// are all sent to this address with a tag field</summary>
        public string BaseAddress { get; set; }

        /// <summary>Minimum number of confirmations before deposit will post at the exchange</summary>
        public int MinConfirmations { get; set; }
    }
}