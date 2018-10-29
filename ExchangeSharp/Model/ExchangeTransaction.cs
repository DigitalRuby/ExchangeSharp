/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    using System;

    /// <summary>An encapsulation of a deposit or withdrawal to an exchange</summary>
    public sealed class ExchangeTransaction
    {
        /// <summary>The address the transaction was sent to</summary>
        public string Address { get; set; }

        /// <summary>The address tag used for currencies like Ripple</summary>
        public string AddressTag { get; set; }

        /// <summary>The amount of the transaction</summary>
        public decimal Amount { get; set; }

        /// <summary>The transaction identifier on the blockchain</summary>
        public string BlockchainTxId { get; set; }

        /// <summary>Open text field to track notes</summary>
        public string Notes { get; set; }

        /// <summary>The payment identifier on the site</summary>
        public string PaymentId { get; set; }

        /// <summary>A value indicating whether the transaction is pending</summary>
        public TransactionStatus Status { get; set; } = TransactionStatus.Unknown;

        /// <summary>The timestamp of the transaction, should be in UTC</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>The fee on the transaction</summary>
        public decimal TxFee { get; set; }

        /// <summary>The currency name (ex. BTC)</summary>
        public string Currency { get; set; }

        public override string ToString()
        {
            return
                $"{Amount} {Currency} (fee: {TxFee}) sent to Address: {Address ?? "null"} with AddressTag: {AddressTag ?? "null"} BlockchainTxId: {BlockchainTxId ?? "null"} sent at {Timestamp} UTC. Status: {Status}. Exchange paymentId: {PaymentId ?? "null"}. Notes: {Notes ?? "null"}";
        }
    }
}