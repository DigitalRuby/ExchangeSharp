/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public class ExchangeOkexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.okex.com/api/v1";
        public override string Name => ExchangeName.Okex;

        public ExchangeOkexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.ToLowerInvariant().Replace('-', '_');
        }

        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["error_code"] != null)
            {
                throw new APIException(result["error_code"].ToStringInvariant() + ", see https://www.okex.com/rest_request.html error codes");
            }
        }

        private JToken MakeRequestOkex(ref string symbol, string subUrl)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty));
            CheckError(obj);
            return obj;
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data)
        {
            //{"date":"1518043621","ticker":{"high":"0.01878000","vol":"1911074.97335534","last":"0.01817627","low":"0.01813515","buy":"0.01817626","sell":"0.01823447"}}

            JToken ticker = data["ticker"];
            decimal last = ticker["last"].ConvertInvariant<decimal>();
            decimal vol = ticker["vol"].ConvertInvariant<decimal>();
            return new ExchangeTicker
            {
                Ask = ticker["sell"].ConvertInvariant<decimal>(),
                Bid = ticker["buy"].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    PriceAmount = vol,
                    PriceSymbol = symbol,
                    QuantityAmount = vol * last,
                    QuantitySymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(data["date"].ConvertInvariant<long>())
                }
            };
        }

        public override IEnumerable<string> GetSymbols()
        {
            // WTF no symbols end point? Sigh...
            return new string[]
            {
                "ltc_btc", "eth_btc", "etc_btc", "bch_btc", "btc_usdt", "eth_usdt", "ltc_usdt", "etc_usdt", "bch_usdt", "etc_eth", "bt1_btc", "bt2_btc", "btg_btc", "qtum_btc", "hsr_btc", "neo_btc", "gas_btc", "qtum_usdt", "hsr_usdt", "neo_usdt", "gas_usdt"
            };
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            JToken data = MakeRequestOkex(ref symbol, "/ticker.do?symbol=$SYMBOL$");
            return ParseTicker(symbol, data);
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            JToken token = MakeRequestOkex(ref symbol, "/depth.do?symbol=$SYMBOL$");
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray ask in token["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = ask[1].ConvertInvariant<decimal>(), Price = ask[0].ConvertInvariant<decimal>() });
            }
            book.Asks.Sort((a1, a2) => a1.Price.CompareTo(a2.Price));
            foreach (JArray bid in token["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = bid[1].ConvertInvariant<decimal>(), Price = bid[0].ConvertInvariant<decimal>() });
            }
            return book;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            JToken trades = MakeRequestOkex(ref symbol, "/trades.do?symbol=$SYMBOL$");
            foreach (JToken trade in trades)
            {
                // [ { "date": "1367130137", "date_ms": "1367130137000", "price": 787.71, "amount": 0.003, "tid": "230433", "type": "sell" } ]
                yield return new ExchangeTrade
                {
                    Amount = trade["amount"].ConvertInvariant<decimal>(),
                    Price = trade["price"].ConvertInvariant<decimal>(),
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(trade["date_ms"].ConvertInvariant<long>()),
                    Id = trade["tid"].ConvertInvariant<long>(),
                    IsBuy = trade["type"].ToStringInvariant() == "buy"
                };
            }
        }
    }
}
