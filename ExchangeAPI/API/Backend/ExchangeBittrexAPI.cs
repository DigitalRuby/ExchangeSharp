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
    public class ExchangeBittrexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://bittrex.com/api/v1.1";
        public string BaseUrl2 { get; set; } = "https://bittrex.com/api/v2.0";
        public override string Name => ExchangeAPI.ExchangeNameBittrex;

        private string NormalizeSymbol(string symbol)
        {
            return symbol.ToUpperInvariant();
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (payload != null)
            {
                var query = HttpUtility.ParseQueryString(url.Query);
                url.Query = "apikey=" + PublicApiKey + "&nonce=" + DateTime.UtcNow.Ticks + (query.Count == 0 ? string.Empty : "&" + query.ToString());
                return url.Uri;
            }
            return url.Uri;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload != null)
            {
                string url = request.RequestUri.ToString();
                string sign = CryptoUtility.SHA512Sign(url, CryptoUtility.SecureStringToString(PrivateApiKey));
                request.Headers["apisign"] = sign;
            }
        }

        public override string[] GetSymbols()
        {
            List<string> symbols = new List<string>();
            JArray obj = MakeJsonRequest<JObject>("/public/getmarkets")["result"] as JArray;
            if (obj != null)
            {
                foreach (JToken token in obj)
                {
                    symbols.Add(token["MarketName"].Value<string>());
                }
            }
            return symbols.ToArray();
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            JToken ticker = MakeJsonRequest<JObject>("/public/getmarketsummary?market=" + NormalizeSymbol(symbol))["result"][0];
            if (ticker != null)
            {
                return new ExchangeTicker
                {
                    Ask = ticker["Ask"].Value<decimal>(),
                    Bid = ticker["Bid"].Value<decimal>(),
                    Last = ticker["Last"].Value<decimal>(),
                    Volume = new ExchangeVolume
                    {
                        PriceAmount = ticker["BaseVolume"].Value<decimal>(),
                        PriceSymbol = symbol,
                        QuantityAmount = ticker["Volume"].Value<decimal>(),
                        QuantitySymbol = symbol,
                        Timestamp = ticker["TimeStamp"].Value<DateTime>()
                    }
                };
            }
            return null;
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("public/getorderbook?market=" + symbol + "&type=both&limit_bids=" + maxCount + "&limit_asks=" + maxCount)["result"];
            if (obj == null)
            {
                return null;
            }
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = obj["buy"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["Quantity"].Value<decimal>(), Price = token["Rate"].Value<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = obj["sell"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["Quantity"].Value<decimal>(), Price = token["Rate"].Value<decimal>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            // TODO: sinceDateTime is ignored
            // https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=oneMin&_=1499127220008
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/pub/market/GetTicks?marketName=" + symbol + "&tickInterval=oneMin";
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&_=" + DateTime.UtcNow.Ticks;
                }
                JArray obj = MakeJsonRequest<JObject>(url, BaseUrl2)["result"] as JArray;
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = obj.Last["T"].Value<DateTime>();
                }
                foreach (JToken trade in obj)
                {
                    // {"O":0.00106302,"H":0.00106302,"L":0.00106302,"C":0.00106302,"V":80.58638589,"T":"2017-08-18T17:48:00","BV":0.08566493}
                    trades.Add(new ExchangeTrade
                    {
                        Amount = trade["V"].Value<decimal>(),
                        Price = trade["C"].Value<decimal>(),
                        Timestamp = trade["T"].Value<DateTime>(),
                        Id = -1,
                        IsBuy = true
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

        public override IEnumerable<ExchangeTrade> GetRecentTrades(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/public/getmarkethistory?market=" + symbol;
            JArray obj = MakeJsonRequest<JObject>(baseUrl)["result"] as JArray;
            foreach (JToken token in obj)
            {
                yield return new ExchangeTrade
                {
                    Amount = token.Value<decimal>("Quantity"),
                    IsBuy = token.Value<string>("OrderType").Equals("BUY", StringComparison.OrdinalIgnoreCase),
                    Price = token.Value<decimal>("Price"),
                    Timestamp = token.Value<DateTime>("TimeStamp"),
                    Id = token.Value<long>("Id")
                };
            }
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JObject result = MakeJsonRequest<JObject>(url, null, new Dictionary<string, object>());
            if (result["success"].Value<bool>())
            {
                foreach (JToken token in result["result"].Children())
                {
                    currencies.Add(token["Currency"].Value<string>(), token["Available"].Value<decimal>());
                }
            }
            return currencies;
        }

        public override ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy)
        {
            symbol = NormalizeSymbol(symbol);
            string url = (buy ? "/market/buylimit" : "/market/selllimit") + "?market=" + symbol + "&quantity=" + amount + "&rate=" + price;
            JObject result = MakeJsonRequest<JObject>(url, null, new Dictionary<string, object>());
            if (result["success"].Value<bool>())
            {
                string orderId = result["result"]["uuid"].Value<string>();
                return GetOrderDetails(orderId);
            }
            return new ExchangeOrderResult
            {
                Result = ExchangeAPIOrderResult.Error,
                Message = result["message"].Value<string>()
            };
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            string url = "/account/getorder?uuid=" + orderId;
            JObject result = MakeJsonRequest<JObject>(url, null, new Dictionary<string, object>());
            if (!result["success"].Value<bool>())
            {
                return new ExchangeOrderResult { Result = ExchangeAPIOrderResult.Error, Message = result["message"].Value<string>() };
            }
            decimal amount = result.Value<decimal>("Quantity");
            decimal remaining = result.Value<decimal>("QuantityRemaining");
            decimal amountFilled = amount - remaining;
            return new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                AveragePrice = result.Value<decimal>("Price"),
                Message = string.Empty,
                OrderId = orderId,
                Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially)),
                OrderDate = result["Opened"].Value<DateTime>(),
                Symbol = result["Exchange"].Value<string>()
            };
        }

        public override string CancelOrder(string orderId)
        {
            JObject result = MakeJsonRequest<JObject>("/market/cancel?uuid=" + orderId);
            if (result.Value<bool>("success"))
            {
                return null;
            }
            return result.Value<string>("message");
        }
    }
}
