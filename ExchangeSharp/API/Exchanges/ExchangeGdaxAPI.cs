﻿/*
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
    public class ExchangeGdaxAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.gdax.com";
        public override string Name => ExchangeName.GDAX;

        /// <summary>
        /// The response will also contain a CB-AFTER header which will return the cursor id to use in your next request for the page after this one. The page after is an older page and not one that happened after this one in chronological time.
        /// </summary>
        private string cursorAfter;

        /// <summary>
        /// The response will contain a CB-BEFORE header which will return the cursor id to use in your next request for the page before the current one. The page before is a newer page and not one that happened before in chronological time.
        /// </summary>
        private string cursorBefore;

        private ExchangeOrderResult ParseOrder(JToken result)
        {
            decimal executedValue = result["executed_value"].ConvertInvariant<decimal>();
            decimal amountFilled = result["filled_size"].ConvertInvariant<decimal>();
            decimal amount = result["size"].ConvertInvariant<decimal>(amountFilled);
            decimal averagePrice = (executedValue <= 0m ? 0m : executedValue / amountFilled);
            ExchangeOrderResult order = new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                AveragePrice = averagePrice,
                IsBuy = (result["side"].ToStringInvariant() == "buy"),
                OrderDate = result["created_at"].ConvertInvariant<DateTime>(),
                Symbol = result["product_id"].ToStringInvariant(),
                OrderId = result["id"].ToStringInvariant()
            };
            switch (result["status"].ToStringInvariant())
            {
                case "pending":
                    order.Result = ExchangeAPIOrderResult.Pending;
                    break;
                case "active":
                case "open":
                    if (order.Amount == order.AmountFilled)
                    {
                        order.Result = ExchangeAPIOrderResult.Filled;
                    }
                    else if (order.AmountFilled > 0.0m)
                    {
                        order.Result = ExchangeAPIOrderResult.FilledPartially;
                    }
                    else
                    {
                        order.Result = ExchangeAPIOrderResult.Pending;
                    }
                    break;
                case "done":
                case "settled":
                    order.Result = ExchangeAPIOrderResult.Filled;
                    break;
                case "cancelled":
                case "canceled":
                    order.Result = ExchangeAPIOrderResult.Canceled;
                    break;
                default:
                    order.Result = ExchangeAPIOrderResult.Unknown;
                    break;
            }
            return order;
        }

        protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
        {
            return base.CanMakeAuthenticatedRequest(payload) && Passphrase != null;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (!CanMakeAuthenticatedRequest(payload))
            {
                return;
            }

            // gdax is funny and wants a seconds double for the nonce, weird... we convert it to double and back to string invariantly to ensure decimal dot is used and not comma
            string timestamp = payload["nonce"].ToStringInvariant();
            payload.Remove("nonce");
            string form = GetJsonForPayload(payload);
            byte[] secret = CryptoUtility.SecureStringToBytesBase64Decode(PrivateApiKey);
            string toHash = timestamp + request.Method.ToUpper() + request.RequestUri.PathAndQuery + form;
            string signatureBase64String = CryptoUtility.SHA256SignBase64(toHash, secret);
            secret = null;
            toHash = null;
            request.Headers["CB-ACCESS-KEY"] = PublicApiKey.ToUnsecureString();
            request.Headers["CB-ACCESS-SIGN"] = signatureBase64String;
            request.Headers["CB-ACCESS-TIMESTAMP"] = timestamp;
            request.Headers["CB-ACCESS-PASSPHRASE"] = CryptoUtility.SecureStringToString(Passphrase);
            WriteFormToRequest(request, form);
        }

        protected override void ProcessResponse(HttpWebResponse response)
        {
            base.ProcessResponse(response);
            cursorAfter = response.Headers["cb-after"];
            cursorBefore = response.Headers["cb-before"];
        }

        public ExchangeGdaxAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixSeconds;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return symbol?.Replace('_', '-').ToUpperInvariant();
        }

        public override IEnumerable<string> GetSymbols()
        {
            Dictionary<string, string>[] symbols = MakeJsonRequest<Dictionary<string, string>[]>("/products");
            List<string> symbolList = new List<string>();
            foreach (Dictionary<string, string> symbol in symbols)
            {
                symbolList.Add(symbol["id"]);
            }
            return symbolList.ToArray();
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            Dictionary<string, string> ticker = MakeJsonRequest<Dictionary<string, string>>("/products/" + symbol + "/ticker");
            decimal volume = Convert.ToDecimal(ticker["volume"], System.Globalization.CultureInfo.InvariantCulture);
            DateTime timestamp = DateTime.Parse(ticker["time"]);
            decimal price = Convert.ToDecimal(ticker["price"], System.Globalization.CultureInfo.InvariantCulture);
            return new ExchangeTicker
            {
                Ask = Convert.ToDecimal(ticker["ask"], System.Globalization.CultureInfo.InvariantCulture),
                Bid = Convert.ToDecimal(ticker["bid"], System.Globalization.CultureInfo.InvariantCulture),
                Last = price,
                Volume = new ExchangeVolume { PriceAmount = volume, PriceSymbol = symbol, QuantityAmount = volume * price, QuantitySymbol = symbol, Timestamp = timestamp }
            };
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            string baseUrl = "/products/" + symbol.ToUpperInvariant() + "/candles?granularity=" + (sinceDateTime == null ? "3600.0" : "60.0");
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            decimal[][] tradeChunk;
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&start=" + System.Web.HttpUtility.UrlEncode(sinceDateTime.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
                    url += "&end=" + System.Web.HttpUtility.UrlEncode(sinceDateTime.Value.AddMinutes(5.0).ToString("s", System.Globalization.CultureInfo.InvariantCulture));
                }
                tradeChunk = MakeJsonRequest<decimal[][]>(url);
                if (tradeChunk == null || tradeChunk.Length == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeSeconds((double)tradeChunk[0][0]);
                }
                foreach (decimal[] tradeChunkPiece in tradeChunk)
                {
                    trades.Add(new ExchangeTrade { Amount = tradeChunkPiece[5], IsBuy = true, Price = tradeChunkPiece[3], Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds((double)tradeChunkPiece[0]), Id = 0 });
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
            string baseUrl = "/products/" + symbol.ToUpperInvariant() + "/trades";
            Dictionary<string, object>[] trades = MakeJsonRequest<Dictionary<string, object>[]>(baseUrl);
            List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
            foreach (Dictionary<string, object> trade in trades)
            {
                tradeList.Add(new ExchangeTrade
                {
                    Amount = trade["size"].ConvertInvariant<decimal>(),
                    IsBuy = trade["side"].ToStringInvariant() == "buy",
                    Price = trade["price"].ConvertInvariant<decimal>(),
                    Timestamp = (DateTime)trade["time"],
                    Id = trade["trade_id"].ConvertInvariant<long>()
                });
            }
            foreach (ExchangeTrade trade in tradeList)
            {
                yield return trade;
            }
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 50)
        {
            string url = "/products/" + symbol.ToUpperInvariant() + "/book?level=2";
            ExchangeOrderBook orders = new ExchangeOrderBook();
            Dictionary<string, object> books = MakeJsonRequest<Dictionary<string, object>>(url);
            JArray asks = books["asks"] as JArray;
            JArray bids = books["bids"] as JArray;
            foreach (JArray ask in asks)
            {
                orders.Asks.Add(new ExchangeOrderPrice { Amount = ask[1].ConvertInvariant<decimal>(), Price = ask[0].ConvertInvariant<decimal>() });
            }
            foreach (JArray bid in bids)
            {
                orders.Bids.Add(new ExchangeOrderPrice { Amount = bid[1].ConvertInvariant<decimal>(), Price = bid[0].ConvertInvariant<decimal>() });
            }
            return orders;
        }

        public override IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null)
        {
            // /products/<product-id>/candles
            // https://api.gdax.com/products/LTC-BTC/candles?granularity=86400&start=2017-12-04T18:15:33&end=2017-12-11T18:15:33
            List<MarketCandle> candles = new List<MarketCandle>();
            symbol = NormalizeSymbol(symbol);
            string url = "/products/" + symbol + "/candles?granularity=" + periodSeconds;
            if (startDate == null)
            {
                startDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1.0));
            }
            url += "&start=" + startDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            if (endDate == null)
            {
                endDate = DateTime.UtcNow;
            }
            url += "&end=" + endDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

            // time, low, high, open, close, volume
            JToken token = MakeJsonRequest<JToken>(url);
            foreach (JArray candle in token)
            {
                candles.Add(new MarketCandle
                {
                    ClosePrice = candle[4].ConvertInvariant<decimal>(),
                    ExchangeName = Name,
                    HighPrice = candle[2].ConvertInvariant<decimal>(),
                    LowPrice = candle[1].ConvertInvariant<decimal>(),
                    Name = symbol,
                    OpenPrice = candle[3].ConvertInvariant<decimal>(),
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(candle[0].ConvertInvariant<long>()),
                    VolumePrice = candle[5].ConvertInvariant<double>(),
                    VolumeQuantity = candle[5].ConvertInvariant<double>() * candle[4].ConvertInvariant<double>()
                });
            }
            // re-sort in ascending order
            candles.Sort((c1, c2) => c1.Timestamp.CompareTo(c2.Timestamp));
            return candles;
        }

        public override Dictionary<string, decimal> GetAmounts()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray array = MakeJsonRequest<JArray>("/accounts", null, GetNoncePayload());
            foreach (JToken token in array)
            {
                decimal amount = token["balance"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[token["currency"].ToStringInvariant()] = amount;
                }
            }
            return amounts;
        }

        public override Dictionary<string, decimal> GetAmountsAvailableToTrade()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray array = MakeJsonRequest<JArray>("/accounts", null, GetNoncePayload());
            foreach (JToken token in array)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[token["currency"].ToStringInvariant()] = amount;
                }
            }
            return amounts;
        }

        public override ExchangeOrderResult PlaceOrder(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "nonce",GenerateNonce() },
                { "type", order.OrderType.ToString().ToLowerInvariant() },
                { "side", (order.IsBuy ? "buy" : "sell") },
                { "product_id", symbol },
                { "size", order.RoundAmount().ToStringInvariant() },
                { "time_in_force", "GTC" } // good til cancel
            };

            if (order.OrderType != OrderType.Market)
            {
                payload.Add("price", order.Price.ToStringInvariant());
            }

            JObject result = MakeJsonRequest<JObject>("/orders", null, payload, "POST");
            return ParseOrder(result);
        }

        public override ExchangeOrderResult GetOrderDetails(string orderId)
        {
            JObject obj = MakeJsonRequest<JObject>("/orders/" + orderId, null, GetNoncePayload(), "GET");
            return ParseOrder(obj);
        }

        public override IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null)
        {
            symbol = NormalizeSymbol(symbol);
            JArray array = MakeJsonRequest<JArray>("orders?status=all" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "&product_id=" + symbol), null, GetNoncePayload());
            foreach (JToken token in array)
            {
                yield return ParseOrder(token);
            }
        }

        public override IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null)
        {
            symbol = NormalizeSymbol(symbol);
            JArray array = MakeJsonRequest<JArray>("orders?status=done" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "&product_id=" + symbol), null, GetNoncePayload());
            foreach (JToken token in array)
            {
                ExchangeOrderResult result = ParseOrder(token);
                if (afterDate == null || result.OrderDate >= afterDate)
                {
                    yield return result;
                }
            }
        }

        public override void CancelOrder(string orderId)
        {
            MakeJsonRequest<JArray>("orders/" + orderId, null, GetNoncePayload(), "DELETE");
        }
    }
}
