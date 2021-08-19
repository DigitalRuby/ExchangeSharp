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

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			/*
				{
					"id": "ETH_USDT",
					"base": "ETH",
					"quote": "USDT",
					"fee": "0.2",
					"min_base_amount": "0.001",
					"min_quote_amount": "1.0",
					"amount_precision": 3,
					"precision": 6,
					"trade_status": "tradable",
					"sell_start": 1516378650,
					"buy_start": 1516378650
				}
			*/

			var markets = new List<ExchangeMarket>();
			JToken obj = await MakeJsonRequestAsync<JToken>("/spot/currency_pairs");

			if (!(obj is null))
			{
				foreach (JToken marketSymbolToken in obj)
				{
					var market = new ExchangeMarket
					{
						MarketSymbol = marketSymbolToken["id"].ToStringUpperInvariant(),
						IsActive = marketSymbolToken["trade_status"].ToStringUpperInvariant() == "tradable",
						QuoteCurrency = marketSymbolToken["quote"].ToStringUpperInvariant(),
						BaseCurrency = marketSymbolToken["base"].ToStringUpperInvariant(),
					};
					int pricePrecision = marketSymbolToken["precision"].ConvertInvariant<int>();
					market.PriceStepSize = (decimal)Math.Pow(0.1, pricePrecision);
					int quantityPrecision = marketSymbolToken["amount_precision"].ConvertInvariant<int>();
					market.QuantityStepSize = (decimal)Math.Pow(0.1, quantityPrecision);

					market.MinTradeSizeInQuoteCurrency = marketSymbolToken["min_quote_amount"].ConvertInvariant<decimal>();
					market.MinTradeSize = marketSymbolToken["min_base_amount"].ConvertInvariant<decimal>();

					markets.Add(market);
				}
			}

			return markets;
		}
	}
}
