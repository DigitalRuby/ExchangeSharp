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
		public override string BaseUrlWebSocket { get; set; } = "wss://api.gemini.com/v2/marketdata";
		public ExchangeGeminiAPI()
        {
            MarketSymbolIsUppercase = false;
            MarketSymbolSeparator = string.Empty;
        }

        private async Task<ExchangeVolume> ParseVolumeAsync(JToken token, string symbol)
        {
            ExchangeVolume vol = new ExchangeVolume();
            JProperty[] props = token.Children<JProperty>().ToArray();
            if (props.Length == 3)
            {
                var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(symbol);
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

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			List<ExchangeMarket> hardcodedSymbols = new List<ExchangeMarket>()
			{
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "btcusd", BaseCurrency = "BTC", QuoteCurrency = "USD",
					MinTradeSize = 0.00001M, QuantityStepSize = 0.00000001M, PriceStepSize = 0.01M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ethusd", BaseCurrency = "ETH", QuoteCurrency = "USD",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.01M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ethbtc", BaseCurrency = "ETH", QuoteCurrency = "BTC",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.00001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "zecusd", BaseCurrency = "ZEC", QuoteCurrency = "USD",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.01M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "zecbtc", BaseCurrency = "ZEC", QuoteCurrency = "BTC",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.00001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "zeceth", BaseCurrency = "ZEC", QuoteCurrency = "ETH",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.0001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "zecbch", BaseCurrency = "ZEC", QuoteCurrency = "BCH",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.0001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "zecltc", BaseCurrency = "ZEC", QuoteCurrency = "LTC",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "bchusd", BaseCurrency = "BCH", QuoteCurrency = "USD",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.01M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "bchbtc", BaseCurrency = "BCH", QuoteCurrency = "BTC",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.00001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "bcheth", BaseCurrency = "BCH", QuoteCurrency = "ETH",
					MinTradeSize = 0.001M, QuantityStepSize = 0.000001M, PriceStepSize = 0.0001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ltcusd", BaseCurrency = "LTC", QuoteCurrency = "USD",
					MinTradeSize = 0.01M, QuantityStepSize = 0.00001M, PriceStepSize = 0.01M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ltcbtc", BaseCurrency = "LTC", QuoteCurrency = "BTC",
					MinTradeSize = 0.01M, QuantityStepSize = 0.00001M, PriceStepSize = 0.00001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ltceth", BaseCurrency = "LTC", QuoteCurrency = "ETH",
					MinTradeSize = 0.01M, QuantityStepSize = 0.00001M, PriceStepSize = 0.0001M},
				new ExchangeMarket() { IsActive = true,
					MarketSymbol = "ltcbch", BaseCurrency = "LTC", QuoteCurrency = "BCH",
					MinTradeSize = 0.01M, QuantityStepSize = 0.00001M, PriceStepSize = 0.0001M},
			};
			// + check to make sure no symbols are missing
			var apiSymbols = await GetMarketSymbolsAsync();
			foreach (var apiSymbol in apiSymbols)
				if (!hardcodedSymbols.Select(m => m.MarketSymbol).Contains(apiSymbol))
					throw new Exception("hardcoded symbols out of date, please send a PR on GitHub to update.");
			foreach (var hardcodedSymbol in hardcodedSymbols)
				if (!apiSymbols.Contains(hardcodedSymbol.MarketSymbol))
					throw new Exception("hardcoded symbols out of date, please send a PR on GitHub to update.");
			return hardcodedSymbols;
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
            t.Volume = await ParseVolumeAsync(obj["volume"], marketSymbol);
            return t;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/book/" + marketSymbol + "?limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(obj, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
            {
                Callback = callback,
                DirectionIsBackwards = false,
                EndDate = endDate,
                ParseFunction = (JToken token) => token.ParseTrade("amount", "price", "type", "timestampms", TimestampType.UnixMilliseconds, idKey: "tid"),
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

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			//{
			//  "type": "l2_updates",
			//  "symbol": "BTCUSD",
			//  "changes": [

			//	[
			//	  "buy",
			//	  "9122.04",
			//	  "0.00121425"
			//	],
			//	...,
			//	[
			//	  "sell",
			//	  "9122.07",
			//	  "0.98942292"
			//	]
			//	...
			//  ],
			//  "trades": [
			//	  {
			//		  "type": "trade",
			//		  "symbol": "BTCUSD",
			//		  "event_id": 169841458,
			//		  "timestamp": 1560976400428,
			//		  "price": "9122.04",
			//		  "quantity": "0.0073173",
			//		  "side": "sell"

			//	  },
			//	  ...
			//  ],
			//  "auction_events": [
			//	  {
			//		  "type": "auction_result",
			//		  "symbol": "BTCUSD",
			//		  "time_ms": 1560974400000,
			//		  "result": "success",
			//		  "highest_bid_price": "9150.80",
			//		  "lowest_ask_price": "9150.81",
			//		  "collar_price": "9146.93",
			//		  "auction_price": "9145.00",
			//		  "auction_quantity": "470.10390845"

			//	  },
			//	  {
			//		"type": "auction_indicative",
			//		"symbol": "BTCUSD",
			//		"time_ms": 1560974385000,
			//		"result": "success",
			//		"highest_bid_price": "9150.80",
			//		"lowest_ask_price": "9150.81",
			//		"collar_price": "9146.84",
			//		"auction_price": "9134.04",
			//		"auction_quantity": "389.3094317"
			//	  },
			//	...
			//  ]
			//}

			//{
			//	"type": "trade",
			//	"symbol": "BTCUSD",
			//	"event_id": 3575573053,
			//	“timestamp”: 151231241,
			//	"price": "9004.21000000",
			//	"quantity": "0.09110000",
			//	"side": "buy"
			//}
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(BaseUrlWebSocket, messageCallback: async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["result"].ToStringInvariant() == "error")
				{ // {{  "result": "error",  "reason": "InvalidJson"}}
					Logger.Info(token["reason"].ToStringInvariant());
				}
				else if (token["type"].ToStringInvariant() == "l2_updates")
				{
					string marketSymbol = token["symbol"].ToStringInvariant();
					var tradesToken = token["trades"];
					if (tradesToken != null) foreach (var tradeToken in tradesToken)
					{
						var trade = parseTrade(tradeToken);
						trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
						await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
					}
				}
				else if (token["type"].ToStringInvariant() == "trade")
				{
					string marketSymbol = token["symbol"].ToStringInvariant();
					var trade = parseTrade(token);
					await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
				}
			}, connectCallback: async (_socket) =>
			{
				//{ "type": "subscribe","subscriptions":[{ "name":"l2","symbols":["BTCUSD","ETHUSD","ETHBTC"]}]}
				await _socket.SendMessageAsync(new {
						type = "subscribe", subscriptions = new[] { new { name = "l2", symbols = marketSymbols } } });
			});
			ExchangeTrade parseTrade(JToken token) => token.ParseTrade(
							amountKey: "quantity", priceKey: "price",
							typeKey: "side", timestampKey: "timestamp",
							TimestampType.UnixMilliseconds, idKey: "event_id");
		}
	}

    public partial class ExchangeName { public const string Gemini = "Gemini"; }
}
