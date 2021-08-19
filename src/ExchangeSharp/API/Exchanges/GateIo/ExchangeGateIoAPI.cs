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
using System.Linq;
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

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			var json = await MakeJsonRequestAsync<JToken>("/spot/tickers");

			var tickers = json.Select(tickerToken => ParseTicker(tickerToken))
				.Select(ticker => new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker))
				.ToList();

			return tickers;
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol, int? limit = null)
		{
			var trades = new List<ExchangeTrade>();
			int maxRequestLimit = (limit == null || limit < 1 || limit > 100) ? 100 : (int)limit;
			var json = await MakeJsonRequestAsync<JToken>($"/spot/trades?currency_pair={symbol}&limit={maxRequestLimit}");

			foreach (JToken tradeToken in json)
			{
				/*
					{
						"id": "1232893232",
						"create_time": "1548000000",
						"create_time_ms": "1548000000123.456",
						"order_id": "4128442423",
						"side": "buy",
						"role": "maker",
						"amount": "0.15",
						"price": "0.03",
						"fee": "0.0005",
						"fee_currency": "ETH",
						"point_fee": "0",
						"gt_fee": "0"
					}
				*/

				trades.Add(tradeToken.ParseTrade("amount", "price", "side", "create_time_ms", TimestampType.UnixMillisecondsDouble, "id"));
			}
			return trades;
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

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
		{
			var json = await MakeJsonRequestAsync<JToken>($"/spot/tickers?currency_pair={symbol}");
			return ParseTicker(json.First());
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
		{
			var json = await MakeJsonRequestAsync<JToken>($"/spot/order_book?currency_pair={symbol}");

			/*
				{
				"id": 123456,
				"current": 1623898993123,
				"update": 1623898993121,
				"asks": [
					[
						"1.52",
						"1.151"
					],
					[
						"1.53",
						"1.218"
					]
				],
				"bids": [
					[
						"1.17",
						"201.863"
					],
					[
						"1.16",
						"725.464"
					]
				]
				}
			*/

			var orderBook = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(json, sequence: "current", maxCount: maxCount);
			orderBook.LastUpdatedUtc = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(json["current"].ConvertInvariant<long>());
			return orderBook;
		}

		private ExchangeTicker ParseTicker(JToken tickerToken)
		{
			bool IsEmptyString(JToken token) => token.Type == JTokenType.String && token.ToObject<string>() == string.Empty;

			/*
				{
					"currency_pair": "BTC3L_USDT",
					"last": "2.46140352",
					"lowest_ask": "2.477",
					"highest_bid": "2.4606821",
					"change_percentage": "-8.91",
					"base_volume": "656614.0845820589",
					"quote_volume": "1602221.66468375534639404191",
					"high_24h": "2.7431",
					"low_24h": "1.9863",
					"etf_net_value": "2.46316141",
					"etf_pre_net_value": "2.43201848",
					"etf_pre_timestamp": 1611244800,
					"etf_leverage": "2.2803019447281203"
				}
			*/

			return new ExchangeTicker
			{
				MarketSymbol = tickerToken["currency_pair"].ToStringInvariant(),
				Bid = IsEmptyString(tickerToken["lowest_ask"]) ? default : tickerToken["lowest_ask"].ConvertInvariant<decimal>(),
				Ask = IsEmptyString(tickerToken["highest_bid"]) ? default : tickerToken["highest_bid"].ConvertInvariant<decimal>(),
				Last = tickerToken["last"].ConvertInvariant<decimal>(),
				ApiResponse = tickerToken
			};
		}
	}
}
