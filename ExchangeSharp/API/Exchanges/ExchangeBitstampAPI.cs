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
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace ExchangeSharp
{
    public class ExchangeBitstampAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitstamp.net/api/v2";
        public override string Name => ExchangeName.Bitstamp;

        private JToken MakeBitstampRequest(string subUrl)
        {
            JToken token = MakeJsonRequest<JToken>(subUrl);
            if (!(token is JArray) && token["error"] != null)
            {
                throw new APIException(token["error"].ToString());
            }
            return token;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.Replace("/", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        public override IEnumerable<string> GetSymbols()
        {
            foreach (JToken token in MakeBitstampRequest("/trading-pairs-info"))
            {
                yield return (string)token["url_symbol"];
            }
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            // {"high": "0.10948945", "last": "0.10121817", "timestamp": "1513387486", "bid": "0.10112165", "vwap": "0.09958913", "volume": "9954.37332614", "low": "0.09100000", "ask": "0.10198408", "open": "0.10250028"}
            symbol = NormalizeSymbol(symbol);
            JToken token = MakeBitstampRequest("/ticker/" + symbol);
            return new ExchangeTicker
            {
                Ask = (decimal)token["ask"],
                Bid = (decimal)token["bid"],
                Last = (decimal)token["last"],
                Volume = new ExchangeVolume
                {
                    PriceAmount = (decimal)token["volume"],
                    PriceSymbol = symbol,
                    QuantityAmount = (decimal)token["volume"] * (decimal)token["last"],
                    QuantitySymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds((long)token["timestamp"])
                }
            };
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken token = MakeBitstampRequest("/order_book/" + symbol);
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray ask in token["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = (decimal)ask[1], Price = (decimal)ask[0] });
            }
            foreach (JArray bid in token["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = (decimal)bid[1], Price = (decimal)bid[0] });
            }
            return book;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            // [{"date": "1513387997", "tid": "33734815", "price": "0.01724547", "type": "1", "amount": "5.56481714"}]
            symbol = NormalizeSymbol(symbol);
            JToken token = MakeBitstampRequest("/transactions/" + symbol);
            foreach (JToken trade in token)
            {
                yield return new ExchangeTrade
                {
                    Amount = (decimal)trade["amount"],
                    Id = (long)trade["tid"],
                    IsBuy = (string)trade["type"] == "0",
                    Price = (decimal)trade["price"],
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds((long)trade["date"])
                };
            }
        }
    }
}
