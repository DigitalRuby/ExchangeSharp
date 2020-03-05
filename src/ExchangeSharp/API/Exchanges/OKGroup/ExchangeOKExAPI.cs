/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using ExchangeSharp.OKGroup;

namespace ExchangeSharp
{
    public sealed partial class ExchangeOKExAPI : OKGroupCommon
	{
        public override string BaseUrl { get; set; } = "https://www.okex.com/api/v1";
		public override string BaseUrlV2 { get; set; } = "https://www.okex.com/v2/spot";
		public override string BaseUrlV3 { get; set; } = "https://www.okex.com/api";
        public override string BaseUrlWebSocket { get; set; } = "wss://real.okex.com:8443/ws/v3";
		protected override bool IsFuturesAndSwapEnabled { get; } = true;
	}

	public partial class ExchangeName { public const string OKEx = "OKEx"; }
}