/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Security;

namespace ExchangeSharp
{
    /// <summary>
    /// Encapsulation of a withdrawal request from an exchange
    /// </summary>
    public sealed class ExchangeWithdrawalRequest
    {
        /// <summary>The address</summary>
        public string Address { get; set; }

        /// <summary>Gets or sets the address tag.</summary>
        public string AddressTag { get; set; }

        /// <summary>Gets or sets the Amount. Secondary address identifier for coins like XRP,XMR etc</summary>
        public decimal Amount { get; set; }

        /// <summary>Description of the withdrawal</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the currency to withdraw, i.e. BTC.</summary>
        public string Currency { get; set; }

        /// <summary>
        /// Whether to take the fee from the amount.
        /// Default: true so requests to withdraw an entire balance don't fail.
        /// </summary>
        public bool TakeFeeFromAmount { get; set; } = true;

        /// <summary>
        /// Password if required by the exchange
        /// </summary>
        public SecureString Password { get; set; }

        /// <summary>
        /// Authentication code if required by the exchange
        /// </summary>
        public SecureString Code { get; set; }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            // 2.75 ETH to 0x1234asdf
            string info = $"{Amount} {Currency} to {Address}";
            if (!string.IsNullOrWhiteSpace(AddressTag))
            {
                info += $" with address tag {AddressTag}";
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                info += $" Description: {Description}";
            }

            return info;
        }
    }
}