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
    public class ExchangeGeminiAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.gemini.com/v1";
        public override string Name => ExchangeAPI.ExchangeNameGemini;

        private ExchangeVolume ParseVolume(JToken token)
        {
            ExchangeVolume vol = new ExchangeVolume();
            JProperty[] props = token.Children<JProperty>().ToArray();
            if (props.Length == 3)
            {
                vol.PriceSymbol = props[0].Name;
                vol.PriceAmount = (decimal)props[0].Value;
                vol.QuantitySymbol = props[1].Name;
                vol.QuantityAmount = (decimal)props[1].Value;
                vol.Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)props[2].Value);
            }

            return vol;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload != null)
            {
                payload.Add("request", request.RequestUri.AbsolutePath);
                payload.Add("nonce", DateTime.UtcNow.Ticks);
                string json = JsonConvert.SerializeObject(payload);
                string json64 = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(json));
                string hexSha384 = CryptoUtility.SHA384Sign(json64, CryptoUtility.SecureStringToString(PrivateApiKey));
                request.Headers["X-GEMINI-PAYLOAD"] = json64;
                request.Headers["X-GEMINI-SIGNATURE"] = hexSha384;
                request.Headers["X-GEMINI-APIKEY"] = CryptoUtility.SecureStringToString(PublicApiKey);
                request.Method = "POST";
            }
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol.Replace("-", string.Empty).ToLowerInvariant();
        }

        public override IReadOnlyCollection<string> GetSymbols()
        {
            return MakeJsonRequest<string[]>("/symbols");
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("/pubticker/" + symbol);
            if (obj == null || obj.Count == 0)
            {
                return null;
            }
            ExchangeTicker t = new ExchangeTicker
            {
                Ask = obj.Value<decimal>("ask"),
                Bid = obj.Value<decimal>("bid"),
                Last = obj.Value<decimal>("last")
            };
            t.Volume = ParseVolume(obj["volume"]);
            return t;
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("/book/" + symbol + "?limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            if (obj == null || obj.Count == 0)
            {
                return null;
            }

            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = obj["bids"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["amount"].Value<decimal>(), Price = token["price"].Value<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = obj["asks"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["amount"].Value<decimal>(), Price = token["price"].Value<decimal>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            const int maxCount = 100;
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/trades/" + symbol + "?limit_trades=" + maxCount;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&timestamp=" + CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value).ToString();
                }
                JArray obj = MakeJsonRequest<Newtonsoft.Json.Linq.JArray>(url);
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj.First["timestampms"].Value<long>());
                }
                foreach (JToken token in obj)
                {
                    trades.Add(new ExchangeTrade
                    {
                        Amount = token["amount"].Value<decimal>(),
                        Price = token["price"].Value<decimal>(),
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["timestampms"].Value<long>()),
                        Id = token["tid"].Value<long>(),
                        IsBuy = token["type"].Value<string>() == "buy"
                    });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                foreach (ExchangeTrade t in trades)
                {
                    yield return t;
                }
                trades.Clear();
                if (obj.Count < maxCount || sinceDateTime == null)
                {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray obj = MakeJsonRequest<Newtonsoft.Json.Linq.JArray>("/balances", payload: new Dictionary<string, object>());
            var q = from JToken token in obj
                    select new { Currency = token["currency"].Value<string>(), Available = token["available"].Value<decimal>() };
            foreach (var kv in q)
            {
                lookup[kv.Currency] = kv.Available;
            }
            return lookup;
        }

        public override ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy)
        {
            symbol = NormalizeSymbol(symbol);
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "client_order_id", "GeminiAPI_" + DateTime.UtcNow.ToString("s") },
                { "symbol", symbol },
                { "amount", amount.ToString(CultureInfo.InvariantCulture.NumberFormat) },
                { "price", price.ToString() },
                { "side", (buy ? "buy" : "sell") },
                { "type", "exchange limit" }
            };
            JObject obj = MakeJsonRequest<JObject>("/order/new", null, payload);
            decimal amountFilled = obj.Value<decimal>("executed_amount");
            return new ExchangeOrderResult
            {
                AmountFilled = amountFilled,
                AveragePrice = obj.Value<decimal>("avg_execution_price"),
                Message = string.Empty,
                Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially)),
                OrderId = obj.Value<string>("order_id")
            };
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            JObject result = MakeJsonRequest<JObject>("/order/status", null, new Dictionary<string, object> { { "order_id", orderId } });
            if (result["result"].Value<string>() == "error")
            {
                return new ExchangeOrderResult { Result = ExchangeAPIOrderResult.Error, Message = result["reason"].Value<string>() };
            }
            decimal amount = result["original_amount"].Value<decimal>();
            decimal amountFilled = result["executed_amount"].Value<decimal>();
            return new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                AveragePrice = result["price"].Value<decimal>(),
                Message = string.Empty,
                OrderId = orderId,
                Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially)),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(result["timestampms"].Value<double>()),
                Symbol = result["symbol"].Value<string>(),
                IsBuy = result["side"].Value<string>() == "buy"
            };
        }

        public override void CancelOrder(string orderId)
        {
            JObject result = MakeJsonRequest<JObject>("/order/cancel", null, new Dictionary<string, object>{ { "order_id", orderId } });
            if (result["result"] != null && result["result"].Value<string>() == "error")
            {
                throw new ExchangeAPIException(result["reason"].Value<string>());
            }
        }
    }
}
