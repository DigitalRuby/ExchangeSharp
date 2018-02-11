/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    public class ExchangeWithdrawalRequest
    {
        /// <summary>Gets or sets the asset.</summary>
        /// <value>The asset.</value>
        public string Asset { get; set; }

        /// <summary>Gets or sets to address.</summary>
        /// <value>To address.</value>
        public string ToAddress { get; set; }

        /// <summary>Gets or sets the address tag.</summary>
        /// <value>The address tag.</value>
        public string AddressTag { get; set; }

        /// <summary>Gets or sets the Amount. Secondary address identifier for coins like XRP,XMR etc</summary>
        /// <value>The Amount.</value>
        public decimal Amount { get; set; }

        /// <summary>Description of the address</summary>
        /// <value>The name.</value>
        public string Name { get; set; }
    }
}