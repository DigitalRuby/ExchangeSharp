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

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeZBcomAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "http://api.zb.com/data/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.zb.com:9999/websocket";

        public ExchangeZBcomAPI()
        {
            MarketSymbolSeparator = "_";
            MarketSymbolIsUppercase = false;
        }

        private string NormalizeSymbolWebsocket(string symbol)
        {
            if (symbol == null) return symbol;

            return (symbol ?? string.Empty).ToLowerInvariant().Replace(MarketSymbolSeparator, string.Empty);
        }

        #region publicAPI

        private async Task<Tuple<JToken, string>> MakeRequestZBcomAsync(string marketSymbol, string subUrl, string baseUrl = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", marketSymbol ?? string.Empty), baseUrl);
            return new Tuple<JToken, string>(obj, marketSymbol);
        }

        private async Task<ExchangeTicker> ParseTickerAsync(string symbol, JToken data)
        {
            // {{"ticker":{"vol":"18202.5979","last":"6698.2","sell":"6703.21","buy":"6693.2","high":"6757.69","low":"6512.69"},"date":"1531822098779"}}
            ExchangeTicker ticker = await this.ParseTickerAsync(data["ticker"], symbol, "sell", "buy", "last", "vol");
            ticker.Volume.Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(data["date"].ConvertInvariant<long>());
            return ticker;
        }

        private async Task<ExchangeTicker> ParseTickerV2Async(string symbol, JToken data)
        {
            //{"hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"}}
            return await this.ParseTickerAsync(data.First, symbol, "sell", "buy", "last", "vol");
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var data = await MakeRequestZBcomAsync(string.Empty, "/markets");
            List<string> symbols = new List<string>();
            foreach (JProperty prop in data.Item1)
            {
                symbols.Add(prop.Name);
            }
            return symbols;
        }

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			// GET http://api.zb.cn/data/v1/markets
			// //# Response
			// {
			//     "btc_usdt": {
			//         "amountScale": 4,
			//         "priceScale": 2
			//     },
			//     "ltc_usdt": {
			//         "amountScale": 3,
			//         "priceScale": 2
			//     }
			//     ...
			// }

			var data = await MakeRequestZBcomAsync(string.Empty, "/markets");
			List<ExchangeMarket> symbols = new List<ExchangeMarket>();
			foreach (JProperty prop in data.Item1)
			{
				var split = prop.Name.Split('_');
				var priceScale = prop.First.Value<Int16>("priceScale");
				var priceScaleDecimals = (decimal)Math.Pow(0.1, priceScale);
				var amountScale = prop.First.Value<Int16>("amountScale");
				var amountScaleDecimals = (decimal)Math.Pow(0.1, amountScale);
				symbols.Add(new ExchangeMarket()
				{
					IsActive = true, // ZB.com does not provide this info, assuming all are active
					MarketSymbol = prop.Name,
					BaseCurrency = split[0],
					QuoteCurrency = split[1],
					MinPrice = priceScaleDecimals,
					PriceStepSize = priceScaleDecimals,
					MinTradeSize = amountScaleDecimals,
					QuantityStepSize = amountScaleDecimals,
				});
			}
			return symbols;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            var data = await MakeRequestZBcomAsync(marketSymbol, "/ticker?market=$SYMBOL$");
            return await ParseTickerAsync(data.Item2, data.Item1);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            //{ "hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"},"tvqc":{ "vol":"2125511.1",

            var data = await MakeRequestZBcomAsync(null, "/allTicker", BaseUrl);
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            var symbolLookup = await Cache.Get<Dictionary<string, string>>(nameof(GetMarketSymbolsAsync) + "_Set", async () =>
            {
                // create lookup dictionary of symbol string without separator to symbol string with separator
                IEnumerable<string> symbols = await GetMarketSymbolsAsync();
                Dictionary<string, string> lookup = symbols.ToDictionary((symbol) => symbol.Replace(MarketSymbolSeparator, string.Empty));
                if (lookup.Count == 0)
                {
                    // handle case where exchange burps and sends empty success response
                    return new CachedItem<Dictionary<string, string>>();
                }
                return new CachedItem<Dictionary<string, string>>(lookup, CryptoUtility.UtcNow.AddHours(4.0));
            });
            if (!symbolLookup.Found)
            {
                throw new APIException("Unable to get symbols for exchange " + Name);
            }
            foreach (JToken token in data.Item1)
            {
                //for some reason when returning tickers, the api doesn't include the symbol separator like it does everywhere else so we need to convert it to the correct format
                if (symbolLookup.Value.TryGetValue(token.Path, out string marketSymbol))
                {
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, await ParseTickerV2Async(marketSymbol, token)));
                }
            }
            return tickers;
        }

        protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(string.Empty, async (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token["dataType"].ToStringInvariant() == "trades")
                {
                    var channel = token["channel"].ToStringInvariant();
                    var sArray = channel.Split('_');
                    string marketSymbol = sArray[0];
                    var data = token["data"];
                    var trades = ParseTradesWebsocket(data);
                    foreach (var trade in trades)
                    {
                        await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
                    }
                }
            }, async (_socket) =>
            {
                foreach (var marketSymbol in marketSymbols)
                {
                    string normalizedSymbol = NormalizeSymbolWebsocket(marketSymbol);
                    await _socket.SendMessageAsync(new { @event = "addChannel", channel = normalizedSymbol + "_trades" });
                }
            });
        }

        private IEnumerable<ExchangeTrade> ParseTradesWebsocket(JToken token)
        {
            //{ "amount":"0.0372","price": "7509.7","tid": 153806522,"date": 1532103901,"type": "sell","trade_type": "ask"},{"amount": "0.0076", ...
            var trades = new List<ExchangeTrade>();
            foreach (var t in token)
            {
                trades.Add(t.ParseTrade("amount", "price", "type", "date", TimestampType.UnixSeconds, "tid"));
            }
            return trades;
        }

        #endregion

        #region Error processing

        private string StatusToError(string status)
        {
            switch (status)
            {
                case "1000": return "Success";
                case "1001": return "Error Tips";
                case "1002": return "Internal Error";
                case "1003": return "Validate No Pass";
                case "1004": return "Transaction Password Locked";
                case "1005": return "Transaction Password Error";
                case "1006": return "Real - name verification is pending approval or not approval";
                case "1009": return "This interface is in maintaining";
                case "1010": return "Not open yet";
                case "1012": return "Permission denied.";
                case "2001": return "Insufficient CNY Balance";
                case "2002": return "Insufficient BTC Balance";
                case "2003": return "Insufficient LTC Balance";
                case "2005": return "Insufficient ETH Balance";
                case "2006": return "Insufficient ETC Balance";
                case "2007": return "Insufficient BTS Balance";
                case "2009": return "Insufficient account balance";
                case "3001": return "Not Found Order";
                case "3002": return "Invalid Money";
                case "3003": return "Invalid Amount";
                case "3004": return "No Such User";
                case "3005": return "Invalid Parameters";
                case "3006": return "Invalid IP or Differ From the Bound IP";
                case "3007": return "Invalid Request Time";
                case "3008": return "Not Found Transaction Record";
                case "4001": return "API Interface is locked or not enabled";
                case "4002": return "Request Too Frequently";

                default: return status;
            }
        }

        #endregion
    }

    public partial class ExchangeName { public const string ZBcom = "ZBcom"; }
}
