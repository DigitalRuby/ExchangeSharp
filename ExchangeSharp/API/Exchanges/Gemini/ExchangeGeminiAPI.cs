/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeGeminiAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.gemini.com/v1";

        public ExchangeGeminiAPI()
        {
            MarketSymbolIsUppercase = false;
            MarketSymbolSeparator = string.Empty;
        }

        private ExchangeVolume ParseVolume(JToken token, string symbol)
        {
            ExchangeVolume vol = new ExchangeVolume();
            JProperty[] props = token.Children<JProperty>().ToArray();
            if (props.Length == 3)
            {
                var (baseCurrency, quoteCurrency) = ExchangeMarketSymbolToCurrencies(symbol);
                vol.QuoteCurrency = quoteCurrency.ToUpperInvariant();
                vol.QuoteCurrencyVolume = token[quoteCurrency.ToUpperInvariant()].ConvertInvariant<decimal>();
                vol.BaseCurrency = baseCurrency.ToUpperInvariant();
                vol.BaseCurrencyVolume = token[baseCurrency.ToUpperInvariant()].ConvertInvariant<decimal>();
                vol.Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(props[2].Value.ConvertInvariant<long>());
            }

            return vol;
        }

        private ExchangeOrderResult ParseOrder(JToken result)
        {
            decimal amount = result["original_amount"].ConvertInvariant<decimal>();
            decimal amountFilled = result["executed_amount"].ConvertInvariant<decimal>();
            return new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                Price = result["price"].ConvertInvariant<decimal>(),
                AveragePrice = result["avg_execution_price"].ConvertInvariant<decimal>(),
                Message = string.Empty,
                OrderId = result["id"].ToStringInvariant(),
                Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially)),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(result["timestampms"].ConvertInvariant<double>()),
                MarketSymbol = result["symbol"].ToStringInvariant(),
                IsBuy = result["side"].ToStringInvariant() == "buy"
            };
        }

        protected override Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                payload.Add("request", request.RequestUri.AbsolutePath);
                string json = JsonConvert.SerializeObject(payload);
                string json64 = System.Convert.ToBase64String(json.ToBytesUTF8());
                string hexSha384 = CryptoUtility.SHA384Sign(json64, CryptoUtility.ToUnsecureString(PrivateApiKey));
                request.AddHeader("X-GEMINI-PAYLOAD", json64);
                request.AddHeader("X-GEMINI-SIGNATURE", hexSha384);
                request.AddHeader("X-GEMINI-APIKEY", CryptoUtility.ToUnsecureString(PublicApiKey));
                request.Method = "POST";

                // gemini doesn't put the payload in the post body it puts it in as a http header, so no need to write to request stream
            }
            return base.ProcessRequestAsync(request, payload);
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return await MakeJsonRequestAsync<string[]>("/symbols");
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/pubticker/" + marketSymbol);
            if (obj == null || obj.Count() == 0)
            {
                return null;
            }
            ExchangeTicker t = new ExchangeTicker
            {
                MarketSymbol = marketSymbol,
                Ask = obj["ask"].ConvertInvariant<decimal>(),
                Bid = obj["bid"].ConvertInvariant<decimal>(),
                Last = obj["last"].ConvertInvariant<decimal>()
            };
            t.Volume = ParseVolume(obj["volume"], marketSymbol);
            return t;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/book/" + marketSymbol + "?limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(obj, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
            {
                Callback = callback,
                DirectionIsBackwards = false,
                EndDate = endDate,
                ParseFunction = (JToken token) => token.ParseTrade("amount", "price", "type", "timestampms", TimestampType.UnixMilliseconds),
                StartDate = startDate,
                MarketSymbol = marketSymbol,
                TimestampFunction = (DateTime dt) => ((long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(dt)).ToStringInvariant(),
                Url = "/trades/[marketSymbol]?limit_trades=100&timestamp={0}"
            };
            await state.ProcessHistoricalTrades();
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JArray>("/balances", null, await GetNoncePayloadAsync());
            var q = from JToken token in obj
                    select new { Currency = token["currency"].ToStringInvariant(), Available = token["amount"].ConvertInvariant<decimal>() };
            foreach (var kv in q)
            {
                if (kv.Available > 0m)
                {
                    lookup[kv.Currency] = kv.Available;
                }
            }
            return lookup;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JArray>("/balances", null, await GetNoncePayloadAsync());
            var q = from JToken token in obj
                    select new { Currency = token["currency"].ToStringInvariant(), Available = token["available"].ConvertInvariant<decimal>() };
            foreach (var kv in q)
            {
                if (kv.Available > 0m)
                {
                    lookup[kv.Currency] = kv.Available;
                }
            }
            return lookup;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Market)
            {
                throw new NotSupportedException("Order type " + order.OrderType + " not supported");
            }

            object nonce = await GenerateNonceAsync();
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "nonce", nonce },
                { "client_order_id", "ExchangeSharp_" + CryptoUtility.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture) },
                { "symbol", order.MarketSymbol },
                { "amount", order.RoundAmount().ToStringInvariant() },
                { "price", order.Price.ToStringInvariant() },
                { "side", (order.IsBuy ? "buy" : "sell") },
                { "type", "exchange limit" }
            };
            order.ExtraParameters.CopyTo(payload);
            JToken obj = await MakeJsonRequestAsync<JToken>("/order/new", null, payload);
            return ParseOrder(obj);
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            object nonce = await GenerateNonceAsync();
            JToken result = await MakeJsonRequestAsync<JToken>("/order/status", null, new Dictionary<string, object> { { "nonce", nonce }, { "order_id", orderId } });
            return ParseOrder(result);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            object nonce = await GenerateNonceAsync();
            JToken result = await MakeJsonRequestAsync<JToken>("/orders", null, new Dictionary<string, object> { { "nonce", nonce } });
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    if (marketSymbol == null || token["symbol"].ToStringInvariant() == marketSymbol)
                    {
                        orders.Add(ParseOrder(token));
                    }
                }
            }

            return orders;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            object nonce = await GenerateNonceAsync();
            await MakeJsonRequestAsync<JToken>("/order/cancel", null, new Dictionary<string, object>{ { "nonce", nonce }, { "order_id", orderId } });
        }
    }

    public partial class ExchangeName { public const string Gemini = "Gemini"; }
}
