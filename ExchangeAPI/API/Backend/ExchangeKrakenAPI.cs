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
    public class ExchangeKrakenAPI : ExchangeAPI, IExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.kraken.com";
        public override string Name => ExchangeAPI.ExchangeNameKraken;

        private string NormalizeSymbol(string symbol)
        {
            return symbol.ToUpperInvariant();
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            base.ProcessRequest(request, payload);
            AppendFormToRequest(request, payload);
        }

        public ExchangeKrakenAPI()
        {
            RequestMethod = "POST";
            RequestContentType = "application/x-www-form-urlencoded";
        }

        public override string[] GetSymbols()
        {
            Dictionary<string, object> json = MakeJsonRequest<Dictionary<string, object>>("/0/public/AssetPairs");
            JObject result = json["result"] as JObject;
            return (from prop in result.Children<JProperty>() select prop.Name).ToArray();
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            Dictionary<string, object> json = MakeJsonRequest<Dictionary<string, object>>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", NormalizeSymbol(symbol) } });
            JObject ticker = (json["result"] as JObject)[symbol] as JObject;
            return new ExchangeTicker
            {
                Ask = (double)ticker["a"][0],
                Bid = (double)ticker["b"][0],
                Last = (double)ticker["c"][0],
                Volume = new ExchangeVolume
                {
                    PriceAmount = (double)ticker["v"][0],
                    PriceSymbol = symbol,
                    QuantityAmount = (double)ticker["v"][0],
                    QuantitySymbol = symbol,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("/0/public/Depth?pair=" + symbol + "&count=" + maxCount)["result"][symbol];
            if (obj == null)
            {
                return null;
            }
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = obj["bids"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].Value<double>(), Price = token[0].Value<double>() };
                orders.Bids.Add(order);
            }
            JToken asks = obj["asks"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].Value<double>(), Price = token[0].Value<double>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/0/public/Trades?pair=" + symbol;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&since=" + (long)(CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value) * 1000000.0);
                }
                JToken obj = MakeJsonRequest<JToken>(url)["result"];
                if (obj == null)
                {
                    break;
                }
                JArray outerArray = obj[symbol] as JArray;
                if (outerArray == null || outerArray.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj["last"].Value<double>() / 1000000.0d);
                }
                foreach (JArray array in outerArray.Children<JArray>())
                {
                    trades.Add(new ExchangeTrade
                    {
                        Amount = array[1].Value<double>(),
                        Price = array[0].Value<double>(),
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(array[2].Value<double>()),
                        Id = -1,
                        IsBuy = array[3].Value<char>() == 'b'
                    });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                foreach (ExchangeTrade t in trades)
                {
                    yield return t;
                }
                trades.Clear();
                if (sinceDateTime == null)
                {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
