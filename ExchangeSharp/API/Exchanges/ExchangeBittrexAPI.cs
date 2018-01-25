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

        private JToken CheckError(JToken obj)
        {
            if (obj["success"] == null || !obj["success"].Value<bool>())
            {
                throw new APIException(obj["message"].Value<string>());
            }
            JToken token = obj["result"];
            if (token == null)
            {
                throw new APIException("Null result");
            }
            return token;
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult();
            decimal amount = token.Value<decimal>("Quantity");
            decimal remaining = token.Value<decimal>("QuantityRemaining");
            decimal amountFilled = amount - remaining;
            order.Amount = amount;
            order.AmountFilled = amountFilled;
            order.AveragePrice = token["PricePerUnit"] == null ? token["Price"].Value<decimal>() : token.Value<decimal>("PricePerUnit");
            order.Message = string.Empty;
            order.OrderId = token.Value<string>("OrderUuid");
            order.Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially));
            order.OrderDate = token["Opened"] == null ? token["TimeStamp"].Value<DateTime>() : token["Opened"].Value<DateTime>();
            order.Symbol = token["Exchange"].Value<string>();
            string type = (string)token["OrderType"];
            if (string.IsNullOrWhiteSpace(type))
            {
                type = (string)token["Type"] ?? string.Empty;
            }
            order.IsBuy = type.IndexOf("BUY", StringComparison.OrdinalIgnoreCase) >= 0;
            return order;
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

        public override IEnumerable<string> GetSymbols()
        {
            List<string> symbols = new List<string>();
            JObject obj = MakeJsonRequest<JObject>("/public/getmarkets");
            JToken result = CheckError(obj);
            if (result is JArray array)
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
            JToken result = CheckError(obj);
            JToken ticker = result[0];
            if (ticker != null)
            {
                return new ExchangeTicker
                {
                    Ask = ticker["Ask"].Value<decimal>(),
                    Bid = ticker["Bid"].Value<decimal>(),
                    Last = ticker["Last"].Value<decimal>(),
                    Volume = new ExchangeVolume
                    {
                        PriceAmount = ticker["Volume"].Value<decimal>(),
                        PriceSymbol = symbol,
                        QuantityAmount = ticker["BaseVolume"].Value<decimal>(),
                        QuantitySymbol = symbol,
                        Timestamp = ticker["TimeStamp"].Value<DateTime>()
                    }
                };
            }
            return null;
        }

        public override IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            JObject obj = MakeJsonRequest<Newtonsoft.Json.Linq.JObject>("public/getmarketsummaries");
            JToken tickers = CheckError(obj);
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
            JToken book = CheckError(obj);
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
                JToken result = CheckError(obj);
                JArray array = result as JArray;
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
                Task.Delay(1000).Wait();
            }
        }

        public override IEnumerable<ExchangeTrade> GetRecentTrades(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/public/getmarkethistory?market=" + symbol;
            JObject obj = MakeJsonRequest<JObject>(baseUrl);
            JToken result = CheckError(obj);
            if (result is JArray array && array.Count != 0)
            {
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
        }

        public override IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null)
        {
            // https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=day
            // "{"success":true,"message":"","result":[{"O":0.00011000,"H":0.00060000,"L":0.00011000,"C":0.00039500,"V":5904999.37958770,"T":"2016-06-20T00:00:00","BV":2212.16809610} ] }"
            string periodString;
            switch (periodSeconds)
            {
                case 60: periodString = "oneMin"; break;
                case 300: periodString = "fiveMin"; break;
                case 1800: periodString = "thirtyMin"; break;
                case 3600: periodString = "hour"; break;
                case 86400: periodString = "day"; break;
                case 259200: periodString = "threeDay"; break;
                case 604800: periodString = "week"; break;
                default:
                    if (periodSeconds > 604800)
                    {
                        periodString = "month";
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Period seconds must be 60,300,1800,3600,86400, 259200 or 604800");
                    }
                    break;
            }
            symbol = NormalizeSymbol(symbol);
            endDate = endDate ?? DateTime.UtcNow;
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));
            JToken result = MakeJsonRequest<JToken>("pub/market/GetTicks?marketName=" + symbol + "&tickInterval=" + periodString, BaseUrl2);
            result = CheckError(result);
            if (result is JArray array)
            {
                foreach (JToken jsonCandle in array)
                {
                    MarketCandle candle = new MarketCandle
                    {
                        ClosePrice = (decimal)jsonCandle["C"],
                        ExchangeName = Name,
                        HighPrice = (decimal)jsonCandle["H"],
                        LowPrice = (decimal)jsonCandle["L"],
                        Name = symbol,
                        OpenPrice = (decimal)jsonCandle["O"],
                        PeriodSeconds = periodSeconds,
                        Timestamp = (DateTime)jsonCandle["T"],
                        VolumePrice = (double)jsonCandle["BV"],
                        VolumeQuantity = (double)jsonCandle["V"]
                    };
                    if (candle.Timestamp >= startDate && candle.Timestamp <= endDate)
                    {
                        yield return candle;
                    }
                }
            }
        }

        public override Dictionary<string, decimal> GetAmounts()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    decimal amount = token["Balance"].Value<decimal>();
                    if (amount > 0m)
                    {
                        currencies.Add(token["Currency"].Value<string>(), amount);
                    }
                }
            }
            return currencies;
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    decimal amount = token["Available"].Value<decimal>();
                    if (amount > 0m)
                    {
                        currencies.Add(token["Currency"].Value<string>(), amount);
                    }
                }
            }
            return currencies;
        }

        public override ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy)
        {
            symbol = NormalizeSymbol(symbol);
            string url = (buy ? "/market/buylimit" : "/market/selllimit") + "?market=" + symbol + "&quantity=" +
                RoundAmount(amount).ToString(CultureInfo.InvariantCulture.NumberFormat) + "&rate=" + price.ToString(CultureInfo.InvariantCulture.NumberFormat);
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            string orderId = result["uuid"].Value<string>();
            return new ExchangeOrderResult { Amount = amount, IsBuy = buy, OrderDate = DateTime.UtcNow, OrderId = orderId, Result = ExchangeAPIOrderResult.Pending, Symbol = symbol };
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            string url = "/account/getorder?uuid=" + orderId;
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            return ParseOrder(result);
        }

        public override IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null)
        {
            string url = "/market/getopenorders" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "?market=" + NormalizeSymbol(symbol));
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            JToken result = obj["result"];
            foreach (JToken token in result.Children())
            {
                yield return ParseOrder(token);
            }
        }

        public override IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null)
        {
            string url = "/account/getorderhistory" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "?market=" + NormalizeSymbol(symbol));
            JObject obj = MakeJsonRequest<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            foreach (JToken token in result.Children())
            {
                ExchangeOrderResult order = ParseOrder(token);

                // Bittrex v1.1 API call has no timestamp parameter, sigh...
                if (afterDate == null || order.OrderDate >= afterDate.Value)
                {
                    yield return order;
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
