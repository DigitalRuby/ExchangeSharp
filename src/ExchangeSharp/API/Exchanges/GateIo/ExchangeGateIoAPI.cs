/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExchangeSharp
{
	public partial class ExchangeName { public const string GateIo = "GateIo"; }

	public sealed class ExchangeGateIoAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.gateio.ws/api/v4";
		public override string BaseUrlWebSocket { get; set; } = "wss://api.gateio.ws/ws/v4/";

		public ExchangeGateIoAPI()
		{
			MarketSymbolSeparator = "_";
			RateLimit = new RateGate(300, TimeSpan.FromSeconds(1));
			RequestContentType = "application/json";
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			List<string> symbols = new List<string>();
			JToken? obj = await MakeJsonRequestAsync<JToken>("/spot/currency_pairs");
			if (!(obj is null))
			{
				foreach (JToken token in obj)
				{
					symbols.Add(token["id"].ToStringInvariant());
				}
			}
			return symbols;
		}
	}
}
