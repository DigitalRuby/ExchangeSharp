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
    public class ExchangePoloniexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://poloniex.com";
        public override string Name => ExchangeName.Poloniex;

        private void CheckError(JObject json)
        {
            if (json == null)
            {
                throw new APIException("No response from server");
            }
            JToken error = json["error"];
            if (error != null)
            {
                throw new APIException((string)error);
            }
        }

        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["error"] != null)
            {
                throw new APIException(result["error"].Value<string>());
            }
        }

        private JToken MakePrivateAPIRequest(string command, params object[] parameters)
        {
            Dictionary<string, object> payload = GetNoncePayload();
            payload["command"] = command;
            if (parameters != null && parameters.Length % 2 == 0)
            {
                for (int i = 0; i < parameters.Length;)
                {
                    payload[parameters[i++].ToString()] = parameters[i++];
                }
            }
            JToken result = MakeJsonRequest<JToken>("/tradingApi", null, payload);
            CheckError(result);
            return result;
        }

        private ExchangeOrderResult ParseOrder(JToken result)
        {
            //result = JToken.Parse("{\"orderNumber\":31226040,\"resultingTrades\":[{\"amount\":\"338.8732\",\"date\":\"2014-10-18 23:03:21\",\"rate\":\"0.00000173\",\"total\":\"0.00058625\",\"tradeID\":\"16164\",\"type\":\"buy\"}]}");
            // open order: { "orderNumber": "45549304213", "type": "sell", "rate": "0.01000000", "startingAmount": "1497.74185318", "amount": "1497.74185318", "total": "14.97741853", "date": "2018-01-28 17:07:39", "margin": 0 }
            ExchangeOrderResult order = new ExchangeOrderResult();
            order.OrderId = result["orderNumber"].ToString();
            JToken trades = result["resultingTrades"];
            if (trades != null && trades.Children().Count() != 0)
            {
                decimal tradeCount = (decimal)trades.Children().Count();
                if (tradeCount != 0m)
                {
                    foreach (JToken token in trades)
                    {
                        order.Amount += (decimal)token["amount"];
                        order.AmountFilled = order.Amount;
                        order.AveragePrice += (decimal)token["rate"];
                        if ((string)token["type"] == "buy")
                        {
                            order.IsBuy = true;
                        }
                        if (order.OrderDate == DateTime.MinValue)
                        {
                            order.OrderDate = (DateTime)token["date"];
                        }
                    }
                    order.AveragePrice /= tradeCount;
                }
            }
            else
            {
                if (result["rate"] != null)
                {
                    order.AveragePrice = (decimal)result["rate"];
                }
                if (result["startingAmount"] != null)
                {
                    order.Amount = (decimal)result["startingAmount"];
                }
                if (result["amount"] != null)
                {
                    order.AmountFilled = (decimal)result["amount"] - order.Amount;
                }
                if (result["type"] != null)
                {
                    order.IsBuy = (result["type"].ToString() != "sell");
                }
                if (result["date"] != null)
                {
                    order.OrderDate = (DateTime)result["date"];
                }
            }
            return order;
        }

        private void ParseOrderFromTrades(List<ExchangeOrderResult> orders, JArray trades, string symbol)
        {
            Dictionary<string, ExchangeOrderResult> orderLookup = new Dictionary<string, ExchangeOrderResult>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken token in trades)
            {
                // { "globalTradeID": 25129732, "tradeID": "6325758", "date": "2016-04-05 08:08:40", "rate": "0.02565498", "amount": "0.10000000", "total": "0.00256549", "fee": "0.00200000", "orderNumber": "34225313575", "type": "sell", "category": "exchange" }
                ExchangeOrderResult subOrder = new ExchangeOrderResult();
                subOrder.Amount = (decimal)token["amount"];
                subOrder.AmountFilled = subOrder.Amount;
                subOrder.AveragePrice = (decimal)token["rate"];
                subOrder.IsBuy = (string)token["type"] != "sell";
                subOrder.OrderDate = (DateTime)token["date"];
                subOrder.OrderId = (string)token["orderNumber"];
                subOrder.Result = ExchangeAPIOrderResult.Filled;
                subOrder.Symbol = symbol;
                if (orderLookup.TryGetValue(subOrder.OrderId, out ExchangeOrderResult baseOrder))
                {
                    baseOrder.AppendOrderWithOrder(subOrder);
                }
                else
                {
                    orderLookup[subOrder.OrderId] = subOrder;
                }
            }
            orders.AddRange(orderLookup.Values);
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string form = GetFormForPayload(payload);
                request.Headers["Key"] = PublicApiKey.ToUnsecureString();
                request.Headers["Sign"] = CryptoUtility.SHA512Sign(form, PrivateApiKey.ToUnsecureString());
                request.Method = "POST";
                WriteFormToRequest(request, form);
            }
        }

        public ExchangePoloniexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.ToUpperInvariant().Replace('-', '_');
        }

        public override IEnumerable<string> GetSymbols()
        {
            List<string> symbols = new List<string>();
            var tickers = GetTickers();
            foreach (var kv in tickers)
            {
                symbols.Add(kv.Key);
            }
            return symbols;
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            IEnumerable<KeyValuePair<string, ExchangeTicker>> tickers = GetTickers();
            foreach (var kv in tickers)
            {
                if (kv.Key == symbol)
                {
                    return kv.Value;
                }
            }
            return null;
        }

        public override IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            // {"BTC_LTC":{"last":"0.0251","lowestAsk":"0.02589999","highestBid":"0.0251","percentChange":"0.02390438","baseVolume":"6.16485315","quoteVolume":"245.82513926"}
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JObject obj = MakeJsonRequest<JObject>("/public?command=returnTicker");
            CheckError(obj);
            foreach (JProperty prop in obj.Children())
            {
                string symbol = prop.Name;
                JToken values = prop.Value;
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, new ExchangeTicker
                {
                    Ask = (decimal)values["lowestAsk"],
                    Bid = (decimal)values["highestBid"],
                    Last = (decimal)values["last"],
                    Volume = new ExchangeVolume
                    {
                        PriceAmount = (decimal)values["baseVolume"],
                        PriceSymbol = symbol,
                        QuantityAmount = (decimal)values["quoteVolume"],
                        QuantitySymbol = symbol,
                        Timestamp = DateTime.UtcNow
                    }
                }));
            }
            return tickers;
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            // {"asks":[["0.01021997",22.83117932],["0.01022000",82.3204],["0.01022480",140],["0.01023054",241.06436945],["0.01023057",140]],"bids":[["0.01020233",164.195],["0.01020232",66.22565096],["0.01020200",5],["0.01020010",66.79296968],["0.01020000",490.19563761]],"isFrozen":"0","seq":147171861}
            symbol = NormalizeSymbol(symbol);
            ExchangeOrderBook book = new ExchangeOrderBook();
            JObject obj = MakeJsonRequest<JObject>("/public?command=returnOrderBook&currencyPair=" + symbol + "&depth=" + maxCount);
            CheckError(obj);
            foreach (JArray array in obj["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = (decimal)array[1], Price = (decimal)array[0] });
            }
            foreach (JArray array in obj["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = (decimal)array[1], Price = (decimal)array[0] });
            }
            return book;
        }

        public override IEnumerable<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100)
        {
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            JObject obj = MakeJsonRequest<JObject>("/public?command=returnOrderBook&currencyPair=all&depth=" + maxCount);
            CheckError(obj);
            foreach (JProperty token in obj.Children())
            {
                ExchangeOrderBook book = new ExchangeOrderBook();
                foreach (JArray array in token.First["asks"])
                {
                    book.Asks.Add(new ExchangeOrderPrice { Amount = (decimal)array[1], Price = (decimal)array[0] });
                }
                foreach (JArray array in token.First["bids"])
                {
                    book.Bids.Add(new ExchangeOrderPrice { Amount = (decimal)array[1], Price = (decimal)array[0] });
                }
                books.Add(new KeyValuePair<string, ExchangeOrderBook>(token.Name, book));
            }
            return books;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            // [{"globalTradeID":245321705,"tradeID":11501281,"date":"2017-10-20 17:39:17","type":"buy","rate":"0.01022188","amount":"0.00954454","total":"0.00009756"},...]
            // https://poloniex.com/public?command=returnTradeHistory&currencyPair=BTC_LTC&start=1410158341&end=1410499372
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/public?command=returnTradeHistory&currencyPair=" + symbol;
            string url;
            string dt;
            DateTime timestamp;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&start=" + (long)CryptoUtility.UnixTimestampFromDateTimeSeconds(sinceDateTime.Value) + "&end=" +
                        (long)CryptoUtility.UnixTimestampFromDateTimeSeconds(sinceDateTime.Value.AddDays(1.0));
                }
                JArray obj = MakeJsonRequest<JArray>(url);
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = ((DateTime)obj[0]["date"]).AddSeconds(1.0);
                }
                foreach (JToken child in obj.Children())
                {
                    dt = ((string)child["date"]).Replace(' ', 'T').Trim('Z') + "Z";
                    timestamp = DateTime.Parse(dt).ToUniversalTime();
                    trades.Add(new ExchangeTrade
                    {
                        Amount = (decimal)child["amount"],
                        Price = (decimal)child["rate"],
                        Timestamp = timestamp,
                        Id = (long)child["globalTradeID"],
                        IsBuy = (string)child["type"] == "buy"
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
                Task.Delay(2000).Wait();
            }
        }

        public override IEnumerable<ExchangeTrade> GetRecentTrades(string symbol)
        {
            return GetHistoricalTrades(symbol);
        }

        public override IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null)
        {
            // https://poloniex.com/public?command=returnChartData&currencyPair=BTC_XMR&start=1405699200&end=9999999999&period=14400
            // [{"date":1405699200,"high":0.0045388,"low":0.00403001,"open":0.00404545,"close":0.00435873,"volume":44.34555992,"quoteVolume":10311.88079097,"weightedAverage":0.00430043}]
            symbol = NormalizeSymbol(symbol);
            string url = "/public?command=returnChartData&currencyPair=" + symbol;
            if (startDate != null)
            {
                url += "&start=" + (long)startDate.Value.UnixTimestampFromDateTimeSeconds();
            }
            url += "&end=" + (endDate == null ? long.MaxValue : (long)endDate.Value.UnixTimestampFromDateTimeSeconds());
            url += "&period=" + periodSeconds;
            JToken token = MakeJsonRequest<JToken>(url);
            CheckError(token);
            foreach (JToken candle in token)
            {
                yield return new MarketCandle
                {
                    ClosePrice = (decimal)candle["close"],
                    ExchangeName = Name,
                    HighPrice = (decimal)candle["high"],
                    LowPrice = (decimal)candle["low"],
                    OpenPrice = (decimal)candle["open"],
                    Name = symbol,
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds((long)candle["date"]),
                    VolumePrice = (double)candle["volume"],
                    VolumeQuantity = (double)candle["quoteVolume"],
                    WeightedAverage = (decimal)candle["weightedAverage"]
                };
            }
        }

        public override Dictionary<string, decimal> GetAmounts()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JToken result = MakePrivateAPIRequest("returnCompleteBalances");
            foreach (JProperty child in result.Children())
            {
                decimal amount = (decimal)child.Value["available"];
                if (amount > 0m)
                {
                    amounts[child.Name] = amount;
                }
            }
            return amounts;
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JToken result = MakePrivateAPIRequest("returnBalances");
            foreach (JProperty child in result.Children())
            {
                decimal amount = (decimal)child.Value;
                if (amount > 0m)
                {
                    amounts[child.Name] = amount;
                }
            }
            return amounts;
        }

        public override ExchangeOrderResult PlaceOrder(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);
            JToken result = MakePrivateAPIRequest(order.IsBuy ? "buy" : "sell", "currencyPair", symbol, "rate",
                order.Price.ToString(CultureInfo.InvariantCulture.NumberFormat), "amount", order.RoundAmount().ToString(CultureInfo.InvariantCulture.NumberFormat));
            return ParseOrder(result);
        }

        public override IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null)
        {
            symbol = NormalizeSymbol(symbol);
            if (string.IsNullOrWhiteSpace(symbol))
            {
                symbol = "all";
            }
            JToken result;
            result = MakePrivateAPIRequest("returnOpenOrders", "currencyPair", symbol);
            CheckError(result);
            if (symbol == "all")
            {
                foreach (JProperty prop in result)
                {
                    if (prop.Value is JArray array)
                    {
                        foreach (JToken token in array)
                        {
                            yield return ParseOrder(token);
                        }
                    }
                }
            }
            else if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    yield return ParseOrder(token);
                }
            }
        }

        public override IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null)
        {
            symbol = NormalizeSymbol(symbol);
            if (string.IsNullOrWhiteSpace(symbol))
            {
                symbol = "all";
            }
            JToken result;
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            afterDate = afterDate ?? DateTime.UtcNow.Subtract(TimeSpan.FromDays(365.0));
            long afterTimestamp = (long)afterDate.Value.UnixTimestampFromDateTimeSeconds();
            result = MakePrivateAPIRequest("returnTradeHistory", "currencyPair", symbol, "limit", 10000, "start", afterTimestamp);
            CheckError(result);
            if (symbol != "all")
            {
                ParseOrderFromTrades(orders, result as JArray, symbol);
            }
            else
            {
                foreach (JProperty prop in result)
                {
                    symbol = prop.Name;
                    ParseOrderFromTrades(orders, prop.Value as JArray, symbol);
                }
            }
            return orders;
        }

        public override void CancelOrder(string orderId)
        {
            JToken token = MakePrivateAPIRequest("cancelOrder", "orderNumber", long.Parse(orderId));
            CheckError(token);
            if (token["success"] == null || (int)token["success"] != 1)
            {
                throw new APIException("Failed to cancel order, success was not 1");
            }
        }
    }
}
