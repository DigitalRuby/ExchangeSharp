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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public class ExchangeKrakenAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.kraken.com";
        public override string Name => ExchangeName.Kraken;

        private void CheckError(JObject json)
        {
            if (json["error"] is JArray error && error.Count != 0)
            {
                throw new APIException((string)error[0]);
            }
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload == null || PrivateApiKey == null || PublicApiKey == null || !payload.ContainsKey("nonce"))
            {
                WritePayloadToRequest(request, payload);
            }
            else
            {
                string nonce = payload["nonce"].ToString();
                payload.Remove("nonce");
                string form = GetFormForPayload(payload);
                // nonce must be first on Kraken
                form = "nonce=" + nonce + (string.IsNullOrWhiteSpace(form) ? string.Empty : "&" + form);
                using (SHA256 sha256 = SHA256Managed.Create())
                {
                    string hashString = nonce + form;
                    byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashString));
                    byte[] pathBytes = Encoding.UTF8.GetBytes(request.RequestUri.AbsolutePath);
                    byte[] sigBytes = new byte[sha256Bytes.Length + pathBytes.Length];
                    pathBytes.CopyTo(sigBytes, 0);
                    sha256Bytes.CopyTo(sigBytes, pathBytes.Length);
                    byte[] privateKey = Convert.FromBase64String(CryptoUtility.SecureStringToString(PrivateApiKey));
                    using (System.Security.Cryptography.HMACSHA512 hmac = new System.Security.Cryptography.HMACSHA512(privateKey))
                    {
                        string sign = Convert.ToBase64String(hmac.ComputeHash(sigBytes));
                        request.Headers.Add("API-Sign", sign);
                    }
                }
                request.Headers.Add("API-Key", CryptoUtility.SecureStringToString(PublicApiKey));
                WriteFormToRequest(request, form);
            }
        }

        public ExchangeKrakenAPI()
        {
            RequestMethod = "POST";
            RequestContentType = "application/x-www-form-urlencoded";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.ToUpperInvariant();
        }

        public override IReadOnlyCollection<string> GetSymbols()
        {
            JObject json = MakeJsonRequest<JObject>("/0/public/AssetPairs");
            CheckError(json);
            JToken result = json["result"] as JToken;
            return (from prop in result.Children<JProperty>() select prop.Name).ToArray();
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            JObject json = MakeJsonRequest<JObject>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", NormalizeSymbol(symbol) } });
            CheckError(json);
            JToken ticker = (json["result"] as JToken)[symbol] as JToken;
            return new ExchangeTicker
            {
                Ask = ticker["a"][0].Value<decimal>(),
                Bid = ticker["b"][0].Value<decimal>(),
                Last = ticker["c"][0].Value<decimal>(),
                Volume = new ExchangeVolume
                {
                    PriceAmount = ticker["v"][0].Value<decimal>(),
                    PriceSymbol = symbol,
                    QuantityAmount = ticker["v"][0].Value<decimal>(),
                    QuantitySymbol = symbol,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JObject json = MakeJsonRequest<JObject>("/0/public/Depth?pair=" + symbol + "&count=" + maxCount);
            CheckError(json);
            JToken obj = json["result"][symbol] as JToken;
            if (obj == null)
            {
                return null;
            }
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = obj["bids"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].Value<decimal>(), Price = token[0].Value<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = obj["asks"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].Value<decimal>(), Price = token[0].Value<decimal>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/0/public/Trades?pair=" + symbol;
            string url;
            DateTime timestamp;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&since=" + (long)(CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value) * 1000000.0);
                }
                JObject obj = MakeJsonRequest<JObject>(url);
                CheckError(obj);
                if (obj == null)
                {
                    break;
                }
                JToken result = obj["result"];
                JArray outerArray = result[symbol] as JArray;
                if (outerArray == null || outerArray.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(result["last"].Value<double>() / 1000000.0d);
                }
                foreach (JArray array in outerArray.Children<JArray>())
                {
                    timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(array[2].Value<double>());
                    trades.Add(new ExchangeTrade
                    {
                        Amount = array[1].Value<decimal>(),
                        Price = array[0].Value<decimal>(),
                        Timestamp = timestamp,
                        Id = timestamp.Ticks,
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

        public override ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "pair", symbol },
                { "type", (buy ? "buy" : "sell") },
                { "ordertype", "limit" },
                { "price", price.ToString(CultureInfo.InvariantCulture) },
                { "volume", amount.ToString(CultureInfo.InvariantCulture) },
                { "nonce", DateTime.UtcNow.Ticks }
            };

            JObject obj = MakeJsonRequest<JObject>("/0/private/AddOrder", null, payload);
            CheckError(obj);
            ExchangeOrderResult result = new ExchangeOrderResult();
            if (obj["error"] != null && obj["error"] as JArray != null && (obj["error"] as JArray).Count != 0)
            {
                result.Message = obj["error"][0].ToString();
            }
            result.OrderDate = DateTime.UtcNow;
            if (obj["result"] != null)
            {
                if (obj["result"]["txid"] is JArray array)
                {
                    result.OrderId = (string)array[0];
                }
            }
            return result;
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", DateTime.UtcNow.Ticks }
            };
            JObject obj = MakeJsonRequest<JObject>("/0/private/QueryOrders", null, payload);
            CheckError(obj);
            JToken result = obj["result"];
            ExchangeOrderResult orderResult = new ExchangeOrderResult { OrderId = orderId };
            if (result == null || result[orderId] == null)
            {
                orderResult.Message = "Unknown Error";
                return orderResult;
            }
            result = result[orderId];
            switch (result["status"].Value<string>())
            {
                case "pending": orderResult.Result = ExchangeAPIOrderResult.Pending; break;
                case "open": orderResult.Result = ExchangeAPIOrderResult.FilledPartially; break;
                case "closed": orderResult.Result = ExchangeAPIOrderResult.Filled; break;
                case "canceled": case "expired": orderResult.Result = ExchangeAPIOrderResult.Canceled; break;
                default: orderResult.Result = ExchangeAPIOrderResult.Error; break;
            }
            orderResult.Message = (orderResult.Message ?? result["reason"].Value<string>());
            orderResult.OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(result["opentm"].Value<double>());
            orderResult.Symbol = result["descr"]["pair"].Value<string>();
            orderResult.IsBuy = (result["descr"]["type"].Value<string>() == "buy");
            orderResult.Amount = result["vol"].Value<decimal>();
            orderResult.AmountFilled = result["vol_exec"].Value<decimal>();
            orderResult.AveragePrice = result["price"].Value<decimal>();
            return orderResult;
        }

        public override void CancelOrder(string orderId)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", DateTime.UtcNow.Ticks }
            };
            JObject obj = MakeJsonRequest<JObject>("/0/private/CancelOrder", null, payload);
            CheckError(obj);
        }
    }
}
