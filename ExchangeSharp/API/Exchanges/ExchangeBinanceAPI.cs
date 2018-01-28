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
    public class ExchangeBinanceAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.binance.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://stream.binance.com:9443";
        public string BaseUrlPrivate { get; set; } = "https://www.binance.com/api/v3";
        public override string Name => ExchangeName.Binance;

        public override string NormalizeSymbol(string symbol)
        {
            if (symbol != null)
            {
                symbol = symbol.Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
            }
            return symbol;
        }

        public ExchangeBinanceAPI()
        {
            // give binance plenty of room to accept requests
            RequestWindow = TimeSpan.FromMinutes(15.0);
            NonceStyle = NonceStyle.UnixMilliseconds;
            NonceOffset = TimeSpan.FromSeconds(1.0);
        }

        public override IEnumerable<string> GetSymbols()
        {
            if (ReadCache("GetSymbols", out List<string> symbols))
            {
                return symbols;
            }

            symbols = new List<string>();
            JToken obj = MakeJsonRequest<JToken>("/ticker/allPrices");
            CheckError(obj);
            foreach (JToken token in obj)
            {
                // bug I think in the API returns numbers as symbol names... WTF.
                string symbol = (string)token["symbol"];
                if (!long.TryParse(symbol, out long tmp))
                {
                    symbols.Add(symbol);
                }
            }
            WriteCache("GetSymbols", TimeSpan.FromMinutes(60.0), symbols);
            return symbols;
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<JToken>("/ticker/24hr?symbol=" + symbol);
            CheckError(obj);
            return ParseTicker(symbol, obj);
        }

        public override IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            string symbol;
            JToken obj = MakeJsonRequest<JToken>("/ticker/24hr");
            CheckError(obj);
            foreach (JToken child in obj)
            {
                symbol = child["symbol"].ToString();
                yield return new KeyValuePair<string, ExchangeTicker>(symbol, ParseTicker(symbol, child));
            }
        }

        /// <summary>
        /// Get all tickers via web socket
        /// </summary>
        /// <param name="callback">Callback for tickers</param>
        /// <returns>Task of web socket wrapper - dispose of the wrapper to shutdown the socket</returns>
        public override WebSocketWrapper GetTickersWebSocket(System.Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            return ConnectWebSocket("/stream?streams=!ticker@arr", (msg, _socket) =>
            {
                try
                {
                    JToken token = JToken.Parse(msg);
                    List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();
                    ExchangeTicker ticker;
                    foreach (JToken childToken in token["data"])
                    {
                        ticker = ParseTickerWebSocket(childToken);
                        tickerList.Add(new KeyValuePair<string, ExchangeTicker>(ticker.Volume.PriceSymbol, ticker));
                    }
                    if (tickerList.Count != 0)
                    {
                        callback(tickerList);
                    }
                }
                catch
                {
                }
            });
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<JToken>("/depth?symbol=" + symbol + "&limit=" + maxCount);
            CheckError(obj);
            return ParseOrderBook(obj);
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            /* [ {
            "a": 26129,         // Aggregate tradeId
		    "p": "0.01633102",  // Price
		    "q": "4.70443515",  // Quantity
		    "f": 27781,         // First tradeId
		    "l": 27781,         // Last tradeId
		    "T": 1498793709153, // Timestamp
		    "m": true,          // Was the buyer the maker?
		    "M": true           // Was the trade the best price match?
            } ] */

            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/aggTrades?symbol=" + symbol;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            DateTime cutoff = DateTime.UtcNow;

            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&startTime=" + CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value) +
                        "&endTime=" + CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value + TimeSpan.FromDays(1.0));
                }
                JArray obj = MakeJsonRequest<Newtonsoft.Json.Linq.JArray>(url);
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj.Last["T"].Value<long>());
                    if (sinceDateTime.Value > cutoff)
                    {
                        sinceDateTime = null;
                    }
                }
                foreach (JToken token in obj)
                {
                    // TODO: Binance doesn't provide a buy or sell type, I've put in a request for them to add this
                    trades.Add(new ExchangeTrade
                    {
                        Amount = token["q"].Value<decimal>(),
                        Price = token["p"].Value<decimal>(),
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["T"].Value<long>()),
                        Id = token["a"].Value<long>(),
                        IsBuy = token["m"].Value<bool>()
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

        public override IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null)
        {
            /* [
            [
		    1499040000000,      // Open time
		    "0.01634790",       // Open
		    "0.80000000",       // High
		    "0.01575800",       // Low
		    "0.01577100",       // Close
		    "148976.11427815",  // Volume
		    1499644799999,      // Close time
		    "2434.19055334",    // Quote asset volume
		    308,                // Number of trades
		    "1756.87402397",    // Taker buy base asset volume
		    "28.46694368",      // Taker buy quote asset volume
		    "17928899.62484339" // Can be ignored
		    ]] */

            symbol = NormalizeSymbol(symbol);
            string url = "/klines?symbol=" + symbol;
            if (startDate != null)
            {
                url += "&startTime=" + (long)startDate.Value.UnixTimestampFromDateTimeMilliseconds();
            }
            url += "&endTime=" + (endDate == null ? long.MaxValue : (long)endDate.Value.UnixTimestampFromDateTimeMilliseconds());
            string periodString = CryptoUtility.SecondsToPeriodString(periodSeconds);
            url += "&interval=" + periodString;
            JToken obj = MakeJsonRequest<JToken>(url);
            CheckError(obj);
            foreach (JArray array in obj)
            {
                yield return new MarketCandle
                {
                    ClosePrice = (decimal)array[4],
                    ExchangeName = Name,
                    HighPrice = (decimal)array[2],
                    LowPrice = (decimal)array[3],
                    Name = symbol,
                    OpenPrice = (decimal)array[1],
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)array[0]),
                    VolumePrice = (double)array[5],
                    VolumeQuantity = (double)array[7],
                    WeightedAverage = 0m
                };
            }
        }

        public override Dictionary<string, decimal> GetAmounts()
        {
            JToken token = MakeJsonRequest<JToken>("/account", BaseUrlPrivate, GetNoncePayload());
            CheckError(token);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken balance in token["balances"])
            {
                decimal amount = (decimal)balance["free"] + (decimal)balance["locked"];
                if (amount > 0m)
                {
                    balances[(string)balance["asset"]] = amount;
                }
            }
            return balances;
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            JToken token = MakeJsonRequest<JToken>("/account", BaseUrlPrivate, GetNoncePayload());
            CheckError(token);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken balance in token["balances"])
            {
                decimal amount = (decimal)balance["free"];
                if (amount > 0m)
                {
                    balances[(string)balance["asset"]] = amount;
                }
            }
            return balances;
        }

        public override ExchangeOrderResult PlaceOrder(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);
            Dictionary<string, object> payload = GetNoncePayload();
            payload["symbol"] = symbol;
            payload["side"] = (order.IsBuy ? "BUY" : "SELL");
            payload["type"] = "LIMIT";
            payload["quantity"] = order.RoundAmount();
            payload["price"] = order.Price;
            payload["timeInForce"] = "GTC";
            JToken token = MakeJsonRequest<JToken>("/order", BaseUrlPrivate, payload, "POST");
            CheckError(token);
            return ParseOrder(token);
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            Dictionary<string, object> payload = GetNoncePayload();
            string[] pieces = orderId.Split(',');
            if (pieces.Length != 2)
            {
                throw new InvalidOperationException("Binance single order details request requires the symbol and order id. The order id needs to be the symbol,orderId. I am sorry for this, I cannot control their API implementation which is really bad here.");
            }
            payload["symbol"] = pieces[0];
            payload["orderId"] = pieces[1];
            JToken token = MakeJsonRequest<JToken>("/order", BaseUrlPrivate, payload);
            CheckError(token);
            return ParseOrder(token);
        }

        private IEnumerable<ExchangeOrderResult> GetOpenOrderDetailsForAllSymbols()
        {
            // TODO: This is a HACK, Binance API needs to add a single API call to get all orders for all symbols, terrible...
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Exception ex = null;
            string failedSymbol = null;
            Parallel.ForEach(GetSymbols().Where(s => s.IndexOf("BTC", StringComparison.OrdinalIgnoreCase) >= 0), (s) =>
            {
                try
                {
                    foreach (ExchangeOrderResult order in GetOpenOrderDetails(s))
                    {
                        lock (orders)
                        {
                            orders.Add(order);
                        }
                    }
                }
                catch (Exception _ex)
                {
                    failedSymbol = s;
                    ex = _ex;
                }
            });

            if (ex != null)
            {
                throw new APIException("Failed to get open orders for symbol " + failedSymbol, ex);
            }

            // sort timestamp desc
            orders.Sort((o1, o2) =>
            {
                return o2.OrderDate.CompareTo(o1.OrderDate);
            });
            foreach (ExchangeOrderResult order in orders)
            {
                yield return order;
            }
        }

        public override IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                foreach (ExchangeOrderResult order in GetOpenOrderDetailsForAllSymbols())
                {
                    yield return order;
                }
            }
            else
            {
                Dictionary<string, object> payload = GetNoncePayload();
                payload["symbol"] = NormalizeSymbol(symbol);
                JToken token = MakeJsonRequest<JToken>("/openOrders", BaseUrlPrivate, payload);
                CheckError(token);
                foreach (JToken order in token)
                {
                    yield return ParseOrder(order);
                }
            }
        }

        private IEnumerable<ExchangeOrderResult> GetCompletedOrdersForAllSymbols(DateTime? afterDate)
        {
            // TODO: This is a HACK, Binance API needs to add a single API call to get all orders for all symbols, terrible...
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Exception ex = null;
            string failedSymbol = null;
            Parallel.ForEach(GetSymbols().Where(s => s.IndexOf("BTC", StringComparison.OrdinalIgnoreCase) >= 0), (s) =>
            {
                try
                {
                    foreach (ExchangeOrderResult order in GetCompletedOrderDetails(s, afterDate))
                    {
                        lock (orders)
                        {
                            orders.Add(order);
                        }
                    }
                }
                catch (Exception _ex)
                {
                    failedSymbol = s;
                    ex = _ex;
                }
            });

            if (ex != null)
            {
                throw new APIException("Failed to get completed order details for symbol " + failedSymbol, ex);
            }

            // sort timestamp desc
            orders.Sort((o1, o2) =>
            {
                return o2.OrderDate.CompareTo(o1.OrderDate);
            });
            foreach (ExchangeOrderResult order in orders)
            {
                yield return order;
            }
        }

        public override IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                foreach (ExchangeOrderResult order in GetCompletedOrdersForAllSymbols(afterDate))
                {
                    yield return order;
                }
            }
            else
            {
                Dictionary<string, object> payload = GetNoncePayload();
                payload["symbol"] = NormalizeSymbol(symbol);
                if (afterDate != null)
                {
                    // TODO: timestamp param is causing duplicate request errors which is a bug in the Binance API
                    // payload["timestamp"] = afterDate.Value.UnixTimestampFromDateTimeMilliseconds();
                }
                JToken token = MakeJsonRequest<JToken>("/allOrders", BaseUrlPrivate, payload);
                CheckError(token);
                foreach (JToken order in token)
                {
                    yield return ParseOrder(order);
                }
            }
        }

        public override void CancelOrder(string orderId)
        {
            Dictionary<string, object> payload = GetNoncePayload();
            string[] pieces = orderId.Split(',');
            if (pieces.Length != 2)
            {
                throw new InvalidOperationException("Binance cancel order request requires the order id be the symbol,orderId. I am sorry for this, I cannot control their API implementation which is really bad here.");
            }
            payload["symbol"] = pieces[0];
            payload["orderId"] = pieces[1];
            JToken token = MakeJsonRequest<JToken>("/order", BaseUrlPrivate, payload, "DELETE");
            CheckError(token);
        }

        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["status"] != null && result["code"] != null)
            {
                throw new APIException(result["code"].Value<string>() + ": " + (result["msg"] != null ? result["msg"].Value<string>() : "Unknown Error"));
            }
        }

        private ExchangeTicker ParseTicker(string symbol, JToken token)
        {
            // {"priceChange":"-0.00192300","priceChangePercent":"-4.735","weightedAvgPrice":"0.03980955","prevClosePrice":"0.04056700","lastPrice":"0.03869000","lastQty":"0.69300000","bidPrice":"0.03858500","bidQty":"38.35000000","askPrice":"0.03869000","askQty":"31.90700000","openPrice":"0.04061300","highPrice":"0.04081900","lowPrice":"0.03842000","volume":"128015.84300000","quoteVolume":"5096.25362239","openTime":1512403353766,"closeTime":1512489753766,"firstId":4793094,"lastId":4921546,"count":128453}
            return new ExchangeTicker
            {
                Ask = (decimal)token["askPrice"],
                Bid = (decimal)token["bidPrice"],
                Last = (decimal)token["lastPrice"],
                Volume = new ExchangeVolume
                {
                    PriceAmount = (decimal)token["volume"],
                    PriceSymbol = symbol,
                    QuantityAmount = (decimal)token["quoteVolume"],
                    QuantitySymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)token["closeTime"])
                }
            };
        }

        private ExchangeTicker ParseTickerWebSocket(JToken token)
        {
            return new ExchangeTicker
            {
                Ask = (decimal)token["a"],
                Bid = (decimal)token["b"],
                Last = (decimal)token["c"],
                Volume = new ExchangeVolume
                {
                    PriceAmount = (decimal)token["v"],
                    PriceSymbol = token["s"].ToString(),
                    QuantityAmount = (decimal)token["q"],
                    QuantitySymbol = token["s"].ToString(),
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)token["E"])
                }
            };
        }

        private ExchangeOrderBook ParseOrderBook(JToken token)
        {
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray array in token["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Price = (decimal)array[0], Amount = (decimal)array[1] });
            }
            foreach (JArray array in token["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Price = (decimal)array[0], Amount = (decimal)array[1] });
            }
            return book;
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
              "symbol": "IOTABTC",
              "orderId": 1,
              "clientOrderId": "12345",
              "transactTime": 1510629334993,
              "price": "1.00000000",
              "origQty": "1.00000000",
              "executedQty": "0.00000000",
              "status": "NEW",
              "timeInForce": "GTC",
              "type": "LIMIT",
              "side": "SELL"
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = (decimal)token["origQty"],
                AmountFilled = (decimal)token["executedQty"],
                AveragePrice = (decimal)token["price"],
                IsBuy = (string)token["side"] == "BUY",
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["time"] == null ? (long)token["transactTime"] : (long)token["time"]),
                OrderId = (string)token["orderId"],
                Symbol = (string)token["symbol"]
            };
            switch ((string)token["status"])
            {
                case "NEW":
                    result.Result = ExchangeAPIOrderResult.Pending;
                    break;

                case "PARTIALLY_FILLED":
                    result.Result = ExchangeAPIOrderResult.FilledPartially;
                    break;

                case "FILLED":
                    result.Result = ExchangeAPIOrderResult.Filled;
                    break;

                case "CANCELED":
                case "PENDING_CANCEL":
                case "EXPIRED":
                case "REJECTED":
                    result.Result = ExchangeAPIOrderResult.Canceled;
                    break;

                default:
                    result.Result = ExchangeAPIOrderResult.Error;
                    break;
            }
            return result;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.Headers["X-MBX-APIKEY"] = PublicApiKey.ToUnsecureString();
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
                var query = HttpUtility.ParseQueryString(url.Query);
                string newQuery = "timestamp=" + payload["nonce"].ToString() + (query.Count == 0 ? string.Empty : "&" + query.ToString()) +
                    (payload.Count > 1 ? "&" + GetFormForPayload(payload, false) : string.Empty);
                string signature = CryptoUtility.SHA256Sign(newQuery, CryptoUtility.SecureStringToBytes(PrivateApiKey));
                newQuery += "&signature=" + signature;
                url.Query = newQuery;
                return url.Uri;
            }
            return base.ProcessRequestUrl(url, payload);
        }
    }
}
