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
        public override string Name => ExchangeName.Bittrex;

        private void CheckError(JToken obj)
        {
            if (obj["success"] == null || !obj["success"].Value<bool>())
            {
                throw new APIException(obj["message"].Value<string>());
            }
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult();
            decimal amount = token.Value<decimal>("Quantity");
            decimal remaining = token.Value<decimal>("QuantityRemaining");
            decimal amountFilled = amount - remaining;
            order.Amount = amount;
            order.AmountFilled = amountFilled;
            order.AveragePrice = token.Value<decimal>("Price");
            order.Message = string.Empty;
            order.OrderId = token.Value<string>("OrderUuid");
            order.Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially));
            order.OrderDate = token["Opened"].Value<DateTime>();
            order.Symbol = token["Exchange"].Value<string>();
            string type = (string)token["OrderType"];
            if (string.IsNullOrWhiteSpace(type))
            {
                type = (string)token["Type"] ?? string.Empty;
            }
            order.IsBuy = type.IndexOf("BUY", StringComparison.OrdinalIgnoreCase) >= 0;
            return order;
        }

        private Dictionary<string, object> GetNoncePayload()
        {
            return new Dictionary<string, object>
            {
                { "nonce", DateTime.UtcNow.Ticks }
            };
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
                var query = HttpUtility.ParseQueryString(url.Query);
                url.Query = "apikey=" + PublicApiKey.ToUnsecureString() + "&nonce=" + payload["nonce"].ToString() + (query.Count == 0 ? string.Empty : "&" + query.ToString());
                return url.Uri;
            }
            return url.Uri;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string url = request.RequestUri.ToString();
                string sign = CryptoUtility.SHA512Sign(url, PrivateApiKey.ToUnsecureString());
                request.Headers["apisign"] = sign;
            }
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.ToUpperInvariant();
        }

        public override IReadOnlyCollection<string> GetSymbols()
        {
            List<string> symbols = new List<string>();
            JObject obj = MakeJsonRequest<JObject>("/public/getmarkets");
            CheckError(obj);
            if (obj["result"] is JArray array)
            {
                foreach (JToken token in array)
                {
                    symbols.Add(token["MarketName"].Value<string>());
                }
            }
            return symbols;
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            JObject obj = MakeJsonRequest<JObject>("/public/getmarketsummary?market=" + NormalizeSymbol(symbol));
            CheckError(obj);
            JToken ticker = obj["result"][0];
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

        public override IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            JObject obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("public/getmarketsummaries");
            CheckError(obj);
            JToken tickers = obj["result"];
            if (tickers == null)
            {
                return null;
            }
            string symbol;
            List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (JToken ticker in tickers)
            {
                symbol = (string)ticker["MarketName"];
                ExchangeTicker tickerObj = new ExchangeTicker
                {
                    Ask = (decimal)ticker["Ask"],
                    Bid = (decimal)ticker["Bid"],
                    Last = (decimal)ticker["Last"],
                    Volume = new ExchangeVolume
                    {
                        PriceAmount = (decimal)ticker["BaseVolume"],
                        PriceSymbol = symbol,
                        QuantityAmount = (decimal)ticker["Volume"],
                        QuantitySymbol = symbol,
                        Timestamp = (DateTime)ticker["TimeStamp"]
                    }
                };
                tickerList.Add(new KeyValuePair<string, ExchangeTicker>(symbol, tickerObj));
            }
            return tickerList;
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("public/getorderbook?market=" + symbol + "&type=both&limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            CheckError(obj);
            JToken book = obj["result"];
            if (book == null)
            {
                return null;
            }
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = book["buy"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["Quantity"].Value<decimal>(), Price = token["Rate"].Value<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = book["sell"];
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
                JObject obj = MakeJsonRequest<JObject>(url, BaseUrl2);
                CheckError(obj);
                JArray array = obj["result"] as JArray;
                if (array == null || array.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = array.Last["T"].Value<DateTime>();
                }
                foreach (JToken trade in array)
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
            JObject obj = MakeJsonRequest<JObject>(baseUrl);
            CheckError(obj);
            JArray array = obj["result"] as JArray;
            if (array == null || array.Count == 0)
            {
                yield break;
            }
            foreach (JToken token in array)
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
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            if (obj["result"] is JArray array)
            {
                foreach (JToken token in array)
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
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            string orderId = obj["result"]["uuid"].Value<string>();
            return GetOrderDetails(orderId);
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            string url = "/account/getorder?uuid=" + orderId;
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            JToken result = obj["result"];
            if (result == null)
            {
                return null;
            }
            return ParseOrder(result);
        }

        public override IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null)
        {
            string url = "/market/getopenorders" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "?market=" + NormalizeSymbol(symbol));
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            JToken result = obj["result"];
            if (result != null)
            {
                foreach (JToken token in result.Children())
                {
                    yield return ParseOrder(token);
                }
            }
        }

        public override void CancelOrder(string orderId)
        {
            JObject obj = MakeJsonRequest<JObject>("/market/cancel?uuid=" + orderId, null, GetNoncePayload());
            CheckError(obj);
        }
    }
}
