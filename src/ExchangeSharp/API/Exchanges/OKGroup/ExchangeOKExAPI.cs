/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.OKGroup;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeOKExAPI : OKGroupCommon
	{
		public override string BaseUrl { get; set; } = "https://www.okex.com/api/v1";
		public override string BaseUrlV2 { get; set; } = "https://www.okex.com/v2/spot";
		public override string BaseUrlV3 { get; set; } = "https://www.okex.com/api";
		public override string BaseUrlWebSocket { get; set; } = "wss://real.okex.com:8443/ws/v3";
		public string BaseUrlV5 { get; set; } = "https://okex.com/api/v5";
		protected override bool IsFuturesAndSwapEnabled { get; } = true;

		public override string PeriodSecondsToString(int seconds)
		{
			return CryptoUtility.SecondsToPeriodString(seconds, true);
		}

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			/*
			{
				"code":"0",
				"msg":"",
				"data":[
				{
					"instType":"SWAP",
					"instId":"LTC-USD-SWAP",
					"uly":"LTC-USD",
					"category":"1",
					"baseCcy":"",
					"quoteCcy":"",
					"settleCcy":"LTC",
					"ctVal":"10",
					"ctMult":"1",
					"ctValCcy":"USD",
					"optType":"C",
					"stk":"",
					"listTime":"1597026383085",
					"expTime":"1597026383085",
					"lever":"10",
					"tickSz":"0.01",
					"lotSz":"1",
					"minSz":"1",
					"ctType":"linear",
					"alias":"this_week",
					"state":"live"
				},
					...
			  ]
			}
			*/
			var markets = new List<ExchangeMarket>();
			parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
				"/public/instruments?instType=SPOT", BaseUrlV5));
			if (!IsFuturesAndSwapEnabled)
				return markets;
			parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
				"/public/instruments?instType=FUTURES", BaseUrlV5));
			parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
				"/public/instruments?instType=SWAP", BaseUrlV5));
			return markets;

			void parseMarketSymbolTokens(JToken allMarketSymbolTokens)
			{
				markets.AddRange(from marketSymbolToken in allMarketSymbolTokens
				                 let isSpot = marketSymbolToken["instType"].Value<string>() == "SPOT"
				                 let baseCurrency = isSpot ? marketSymbolToken["baseCcy"].Value<string>() : marketSymbolToken["settleCcy"].Value<string>()
				                 let quoteCurrency = isSpot ? marketSymbolToken["quoteCcy"].Value<string>() : marketSymbolToken["ctValCcy"].Value<string>()
				                 select new ExchangeMarket
				                 {
					                 MarketSymbol = marketSymbolToken["instId"].Value<string>(),
					                 IsActive = marketSymbolToken["state"].Value<string>() == "live",
					                 QuoteCurrency = quoteCurrency,
					                 BaseCurrency = baseCurrency,
					                 PriceStepSize = marketSymbolToken["tickSz"].ConvertInvariant<decimal>(),
					                 MinPrice = marketSymbolToken["tickSz"].ConvertInvariant<decimal>(), // assuming that this is also the min price since it isn't provided explicitly by the exchange
					                 MinTradeSize = marketSymbolToken["minSz"].ConvertInvariant<decimal>(),
					                 QuantityStepSize = marketSymbolToken["lotSz"].ConvertInvariant<decimal>()
				                 });
			}
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var tickerResponse = await MakeJsonRequestAsync<JToken>($"/market/ticker?instId={marketSymbol}", BaseUrlV5);
			var symbol = tickerResponse[0]["instId"].Value<string>();
			return await ParseTickerV5Async(tickerResponse[0], symbol);
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
			await parseData(await MakeJsonRequestAsync<JToken>("/market/tickers?instType=SPOT", BaseUrlV5));
			if (!IsFuturesAndSwapEnabled)
				return tickers;
			await parseData(await MakeJsonRequestAsync<JToken>("/market/tickers?instType=FUTURES", BaseUrlV5));
			await parseData(await MakeJsonRequestAsync<JToken>("/market/tickers?instType=SWAP", BaseUrlV5));
			return tickers;

			async Task parseData(JToken tickerResponse)
			{
				/*{
			"code":"0",
			"msg":"",
			"data":[
				{
				"instType":"SWAP",
				"instId":"LTC-USD-SWAP",
				"last":"9999.99",
				"lastSz":"0.1",
				"askPx":"9999.99",
				"askSz":"11",
				"bidPx":"8888.88",
				"bidSz":"5",
				"open24h":"9000",
				"high24h":"10000",
				"low24h":"8888.88",
				"volCcy24h":"2222",
				"vol24h":"2222",
				"sodUtc0":"0.1",
				"sodUtc8":"0.1",
				"ts":"1597026383085"
			 },
				...
				]
				}
				 */

				foreach (JToken t in tickerResponse)
				{
					var symbol = t["instId"].Value<string>();
					var ticker = await ParseTickerV5Async(t, symbol);
					tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
				}
			}
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol,
			int? limit = null)
		{
			limit ??= 500;
			marketSymbol = NormalizeMarketSymbol(marketSymbol);
			var recentTradesResponse =
				await MakeJsonRequestAsync<JToken>($"/market/trades?instId={marketSymbol}&limit={limit}", BaseUrlV5);
			return recentTradesResponse.Select(t => t.ParseTrade(
					"sz", "px", "side", "ts", TimestampType.UnixMilliseconds, "tradeId"))
				.ToList();
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
		{
			var token = await MakeJsonRequestAsync<JToken>($"/market/books?instId={marketSymbol}&sz={maxCount}", BaseUrlV5);
			return token[0].ParseOrderBookFromJTokenArrays(maxCount: maxCount);
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
		{
			/*
			 {
			    "code":"0",
			    "msg":"",
			    "data":[
			     [
			        "1597026383085", timestamp
			        "3.721", open
			        "3.743", high
			        "3.677", low
			        "3.708", close
			        "8422410", volume
			        "22698348.04828491" volCcy (Quote)
			    ],..
			    ]
			}
			*/

			var candles = new List<MarketCandle>();
			var url = $"/market/history-candles?instId={marketSymbol}";
			if (startDate.HasValue)
				url += "&after=" + (long)startDate.Value.UnixTimestampFromDateTimeMilliseconds();
			if (endDate.HasValue)
				url += "&before=" + (long)endDate.Value.UnixTimestampFromDateTimeMilliseconds();
			if (limit.HasValue)
				url += "&limit=" + limit.Value.ToStringInvariant();
			var periodString = PeriodSecondsToString(periodSeconds);
			url += $"&bar={periodString}";
			var obj = await MakeJsonRequestAsync<JToken>(url, BaseUrlV5);
			foreach (JArray token in obj)
				candles.Add(this.ParseCandle(token, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5, 6));
			return candles;
		}

		private async Task<ExchangeTicker> ParseTickerV5Async(JToken t, string symbol)
		{
			return await this.ParseTickerAsync(
				token: t,
				marketSymbol: symbol,
				askKey: "askPx",
				bidKey: "bidPx",
				lastKey: "last",
				baseVolumeKey: "vol24h",
				quoteVolumeKey: "volCcy24h",
				timestampKey: "ts",
				timestampType: TimestampType.UnixMilliseconds);
		}
	}

	public partial class ExchangeName
	{
		public const string OKEx = "OKEx";
	}
}
