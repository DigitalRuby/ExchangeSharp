/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using ExchangeSharp.BinanceGroup;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExchangeSharp
{
	public sealed class ExchangeBinanceDEXAPI : ExchangeAPI
	{ // unfortunately, Binance DEX doesn't share the same API as Binance and Binance.US, so it shouldn't inherit from BinanceGroupCommon
		public override string BaseUrl { get; set; } = "https://dex.binance.org/api/v1";
		public override string BaseUrlWebSocket { get; set; } = "wss://dex.binance.org/api/ws";
		//public override string BaseUrlPrivate { get; set; } = "https://dex.binance.org/api/v3";
		//public override string WithdrawalUrlPrivate { get; set; } = "https://dex.binance.org/wapi/v3";

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync() =>
			(await GetMarketSymbolsMetadataAsync()).Select(msm => msm.MarketSymbol);

		public override async Task<IEnumerable<ExchangeMarket>> GetMarketSymbolsMetadataAsync()
		{
			// [{"base_asset_symbol":"AERGO-46B","list_price":"0.00350000","lot_size":"1.00000000","quote_asset_symbol":"BNB","tick_size":"0.00000001"}, ...
			var markets = new List<ExchangeMarket>();
			JToken allSymbols = await MakeJsonRequestAsync<JToken>("/markets");
			foreach (JToken marketSymbolToken in allSymbols)
			{
				var QuoteCurrency = marketSymbolToken["quote_asset_symbol"].ToStringUpperInvariant();
				var BaseCurrency = marketSymbolToken["base_asset_symbol"].ToStringUpperInvariant();
				var market = new ExchangeMarket
				{
					MarketSymbol = BaseCurrency + '_' + QuoteCurrency,
					IsActive = true,
					BaseCurrency = BaseCurrency,
					QuoteCurrency = QuoteCurrency,
				};
				market.MinTradeSize = marketSymbolToken["lot_size"].ConvertInvariant<decimal>();
				market.QuantityStepSize = marketSymbolToken["lot_size"].ConvertInvariant<decimal>();

				market.MinPrice = marketSymbolToken["tick_size"].ConvertInvariant<decimal>();
				market.PriceStepSize = marketSymbolToken["tick_size"].ConvertInvariant<decimal>();

				markets.Add(market);
			}

			return markets;
		}

		/// <summary>
		/// Binance DEX doesn't suppport streaming aggregate trades like Binance/US
		/// </summary>
		/// <param name="callback"></param>
		/// <param name="marketSymbols"></param>
		/// <returns></returns>
		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			/* actual data, followed by example data
			{
				"stream": "trades",
				"data": [{
					  "e": "trade",
					  "E": 47515444,
					  "s": "CBM-4B2_BNB",
					  "t": "47515444-0",
					  "p": "0.00000722",
					  "q": "24000.00000000",
					  "b": "F36B58E668004610B5D1DB23A40B496DF27A4D91-91340",
					  "a": "E9F71B7AFD3325590624A692B7DF5B449AA9BA19-91463",
					  "T": 1573417196130225520,
					  "sa": "bnb1a8m3k7haxvj4jp3y56ft0h6mgjd2nwsel6xsx8",
					  "ba": "bnb17d443engqprppdw3mv36gz6fdhe85nv37p2xq4",
					  "tt": 1
				},
				{
					"e": "trade",       // Event type
					"E": 123456795,     // Event time
					"s": "BNB_BTC",     // Symbol
					"t": "12348",       // Trade ID
					"p": "0.001",       // Price
					"q": "100",         // Quantity
					"b": "88",          // Buyer order ID
					"a": "52",          // Seller order ID
					"T": 123456795,     // Trade time
					"sa": "bnb1me5u083m2spzt8pw8vunprnctc8syy64hegrcp", // SellerAddress
					"ba": "bnb1kdr00ydr8xj3ydcd3a8ej2xxn8lkuja7mdunr5" // BuyerAddress
					"tt": 1             //tiekertype 0: Unknown 1: SellTaker 2: BuyTaker 3: BuySurplus 4: SellSurplus 5: Neutral
				}]
			}
            */

			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(string.Empty, messageCallback: async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["stream"].ToStringLowerInvariant() == "trades")
				{
					foreach (var data in token["data"])
					{
						string name = data["s"].ToStringInvariant();
						string marketSymbol = NormalizeMarketSymbol(name);

						await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol,
							data.ParseTradeBinanceDEX(amountKey: "q", priceKey: "p", typeKey: "tt",
								timestampKey: "T", // use trade time (T) instead of event time (E)
								timestampType: TimestampType.UnixNanoseconds, idKey: "t", typeKeyIsBuyValue: "BuyTaker")));
					}
				}
				else if (token["error"] != null)
				{ // {{ "method": "subscribe", "error": { "error": "Invalid symbol(s)" }}}
					Logger.Info(token["error"]["error"].ToStringInvariant());
				}
			}, connectCallback: async (_socket) =>
			{
				await _socket.SendMessageAsync(new { method = "subscribe", topic = "trades", symbols = marketSymbols });
			});
		}
	}

	public partial class ExchangeName { public const string BinanceDEX = "BinanceDEX"; }
}
