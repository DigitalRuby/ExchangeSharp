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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBitstampAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitstamp.net/api/v2";
		public override string BaseUrlWebSocket { get; set; } = "wss://ws.bitstamp.net";

		/// <summary>
		/// Bitstamp private API requires a customer id. Internally this is secured in the PassPhrase property.
		/// </summary>
		public string CustomerId
        {
            get { return Passphrase.ToUnsecureString(); }
            set { Passphrase = value.ToSecureString(); }
        }

        /// <summary>
        /// In order to use private functions of the API, you must set CustomerId by calling constructor with parameter,
        /// or setting it later in the ExchangeBitstampAPI object.
        /// </summary>
        public ExchangeBitstampAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMilliseconds;
            MarketSymbolIsUppercase = false;
            MarketSymbolSeparator = string.Empty;
        }

        /// <summary>
        /// In order to use private functions of the API, you must set CustomerId by calling this constructor with parameter,
        /// or setting it later in the ExchangeBitstampAPI object.
        /// </summary>
        /// <param name="customerId">Customer Id can be found by the link "https://www.bitstamp.net/account/balance/"</param>
        public ExchangeBitstampAPI(string customerId) : this()
        {
            CustomerId = customerId;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (string.IsNullOrWhiteSpace(CustomerId))
                {
                    throw new APIException("Customer ID is not set for Bitstamp");
                }

                // messageToSign = nonce + customer_id + api_key
                string apiKey = PublicApiKey.ToUnsecureString();
                string messageToSign = payload["nonce"].ToStringInvariant() + CustomerId + apiKey;
                string signature = CryptoUtility.SHA256Sign(messageToSign, PrivateApiKey.ToUnsecureString()).ToUpperInvariant();
                payload["signature"] = signature;
                payload["key"] = apiKey;
                await CryptoUtility.WritePayloadFormToRequestAsync(request, payload);
            }
        }

        private async Task<JToken> MakeBitstampRequestAsync(string subUrl)
        {
            JToken token = await MakeJsonRequestAsync<JToken>(subUrl);
            if (!(token is JArray) && token["error"] != null)
            {
                throw new APIException(token["error"].ToStringInvariant());
            }
            return token;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            foreach (JToken token in (await MakeBitstampRequestAsync("/trading-pairs-info")))
            {
                symbols.Add(token["url_symbol"].ToStringInvariant());
            }
            return symbols;
        }

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{ // {"base_decimals": 8, "minimum_order": "5.0 USD", "name": "LTC/USD", "counter_decimals": 2, "trading": "Enabled", "url_symbol": "ltcusd", "description": "Litecoin / U.S. dollar"}
			List<ExchangeMarket> symbols = new List<ExchangeMarket>();
			foreach (JToken token in (await MakeBitstampRequestAsync("/trading-pairs-info")))
			{
				var split = token["name"].ToStringInvariant().Split('/');
				var baseNumDecimals = token["base_decimals"].Value<byte>();
				var baseDecimals = (decimal)Math.Pow(0.1, baseNumDecimals);
				var counterNumDecimals = token["counter_decimals"].Value<byte>();
				var counterDecimals = (decimal)Math.Pow(0.1, counterNumDecimals);
				var minOrderString = token["minimum_order"].ToStringInvariant();
				symbols.Add(new ExchangeMarket()
				{
					MarketSymbol = token["url_symbol"].ToStringInvariant(),
					BaseCurrency = split[0],
					QuoteCurrency = split[1],
					MinTradeSize = baseDecimals, // will likely get overriden by MinTradeSizeInQuoteCurrency
					QuantityStepSize = baseDecimals,
					MinTradeSizeInQuoteCurrency = minOrderString.Split(' ')[0].ConvertInvariant<decimal>(),
					MinPrice = counterDecimals,
					PriceStepSize = counterDecimals,
					IsActive = token["trading"].ToStringLowerInvariant() == "enabled",
				});
			}
			return symbols;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            // {"high": "0.10948945", "last": "0.10121817", "timestamp": "1513387486", "bid": "0.10112165", "vwap": "0.09958913", "volume": "9954.37332614", "low": "0.09100000", "ask": "0.10198408", "open": "0.10250028"}
            JToken token = await MakeBitstampRequestAsync("/ticker/" + marketSymbol);
            return await this.ParseTickerAsync(token, marketSymbol, "ask", "bid", "last", "volume", null, "timestamp", TimestampType.UnixSeconds);
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeBitstampRequestAsync("/order_book/" + marketSymbol);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            // [{"date": "1513387997", "tid": "33734815", "price": "0.01724547", "type": "1", "amount": "5.56481714"}]
            JToken token = await MakeBitstampRequestAsync("/transactions/" + marketSymbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            foreach (JToken trade in token)
            {
                trades.Add(trade.ParseTrade("amount", "price", "type", "date", TimestampType.UnixSeconds, "tid", "0"));
            }
            callback(trades);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "balance");
        }


        protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "fee");
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "available");
        }

        private static Dictionary<string, decimal> ExtractDictionary(JObject responseObject, string key)
        {
            var result = new Dictionary<string, decimal>();
            var suffix = $"_{key}";
            foreach (var property in responseObject)
            {
                if (property.Key.Contains(suffix))
                {
                    decimal value = property.Value.ConvertInvariant<decimal>();
                    if (value == 0)
                    {
                        continue;
                    }

                    result.Add(property.Key.Replace(suffix, "").Trim(), value);
                }
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string action = order.IsBuy ? "buy" : "sell";
            string market = order.OrderType == OrderType.Market ? "/market" : "";
            string url = $"/{action}{market}/{order.MarketSymbol}/";
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            if (order.OrderType != OrderType.Market)
            {
                payload["price"] = order.Price.ToStringInvariant();
            }

            payload["amount"] = order.RoundAmount().ToStringInvariant();
            order.ExtraParameters.CopyTo(payload);

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return new ExchangeOrderResult
            {
                OrderDate = CryptoUtility.UtcNow,
                OrderId = responseObject["id"].ToStringInvariant(),
                IsBuy = order.IsBuy,
                MarketSymbol = order.MarketSymbol
            };
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            //{
            //    "status": "Finished",
            //    "id": 1022694747,
            //    "transactions": [
            //    {
            //        "fee": "0.000002",
            //        "bch": "0.00882714",
            //        "price": "0.12120000",
            //        "datetime": "2018-02-24 14:15:29.133824",
            //        "btc": "0.0010698493680000",
            //        "tid": 56293144,
            //        "type": 2
            //    }]
            //}
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }
            string url = "/order_status/";
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["id"] = orderId;
            JObject result = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");

            // status can be 'In Queue', 'Open' or 'Finished'
            JArray transactions = result["transactions"] as JArray;
            // empty transaction array means that order is InQueue or Open and AmountFilled == 0
            // return empty order in this case. no any additional info available at this point
            if (!transactions.Any()) { return new ExchangeOrderResult() { OrderId = orderId }; }
            JObject first = transactions.First() as JObject;
            List<string> excludeStrings = new List<string>() { "tid", "price", "fee", "datetime", "type", "btc", "usd", "eur" };

            string quoteCurrency;
            string baseCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                // the only 2 cases are BTC-USD and BTC-EUR
                baseCurrency = "btc";
                excludeStrings.RemoveAll(s => s.Equals("usd") || s.Equals("eur"));
                quoteCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            }
            else
            {
                excludeStrings.RemoveAll(s => s.Equals("usd") || s.Equals("eur") || s.Equals("btc"));
                excludeStrings.Add(baseCurrency);
                quoteCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            }
            string _symbol = $"{baseCurrency}-{quoteCurrency}";

            decimal amountFilled = 0, spentQuoteCurrency = 0, price = 0;

            foreach (var t in transactions)
            {
                int type = t["type"].ConvertInvariant<int>();
                if (type != 2) { continue; }
                spentQuoteCurrency += t[quoteCurrency].ConvertInvariant<decimal>();
                amountFilled += t[baseCurrency].ConvertInvariant<decimal>();
                //set price only one time
                if (price == 0)
                {
                    price = t["price"].ConvertInvariant<decimal>();
                }
            }

            // No way to know if order IsBuy, Amount, OrderDate
            return new ExchangeOrderResult()
            {
                AmountFilled = amountFilled,
                MarketSymbol = _symbol,
                AveragePrice = spentQuoteCurrency / amountFilled,
                Price = price,
            };
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/open_orders/all/" : "/open_orders/" + symbol;
            string url = "/open_orders/all/";
            JArray result = await MakeJsonRequestAsync<JArray>(url, null, await GetNoncePayloadAsync(), "POST");
            foreach (JToken token in result)
            {
                //This request doesn't give info about amount filled, use GetOrderDetails(orderId)
                string tokenSymbol = token["currency_pair"].ToStringLowerInvariant().Replace("/", "");
                if (!string.IsNullOrWhiteSpace(tokenSymbol) && !string.IsNullOrWhiteSpace(marketSymbol) && !tokenSymbol.Equals(marketSymbol, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                orders.Add(new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    OrderDate = token["datetime"].ToDateTimeInvariant(),
                    IsBuy = token["type"].ConvertInvariant<int>() == 0,
                    Price = token["price"].ConvertInvariant<decimal>(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    MarketSymbol = tokenSymbol ?? marketSymbol
                });
            }
            return orders;
        }

        public class BitstampTransaction
        {
            public BitstampTransaction(string id, DateTime dateTime, int type, string symbol, decimal fees, string orderId, decimal quantity, decimal price, bool isBuy)
            {
                Id = id;
                DateTime = dateTime;
                Type = type;
                Symbol = symbol;
                Fees = fees;
                OrderId = orderId;
                Quantity = quantity;
                Price = price;
                IsBuy = isBuy;
            }

            public string Id { get; }
            public DateTime DateTime { get; }
            public int Type { get; } //  Transaction type: 0 - deposit; 1 - withdrawal; 2 - market trade; 14 - sub account transfer.
            public string Symbol { get; }
            public decimal Fees { get; }
            public string OrderId { get; }
            public decimal Quantity { get; }
            public decimal Price { get; }
            public bool IsBuy { get; }
        }

        public async Task<IEnumerable<BitstampTransaction>> GetUserTransactionsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            await new SynchronizationContextRemover();

            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/user_transactions/" : "/user_transactions/" + symbol;
            string url = "/user_transactions/";
            var payload = await GetNoncePayloadAsync();
            payload["limit"] = 1000;
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, payload, "POST");

            List<BitstampTransaction> transactions = new List<BitstampTransaction>();

            foreach (var transaction in result as JArray)
            {
                int type = transaction["type"].ConvertInvariant<int>();
                // only type 2 is order transaction type, so we discard all other transactions
                if (type != 2) { continue; }

                string tradingPair = ((JObject)transaction).Properties().FirstOrDefault(p =>
                    !p.Name.Equals("order_id", StringComparison.InvariantCultureIgnoreCase)
                    && p.Name.Contains("_"))?.Name.Replace("_", "-");
                tradingPair = NormalizeMarketSymbol(tradingPair);
                if (!string.IsNullOrWhiteSpace(tradingPair) && !string.IsNullOrWhiteSpace(marketSymbol) && !tradingPair.Equals(marketSymbol))
                {
                    continue;
                }

                var baseCurrency = tradingPair.Trim().Substring(tradingPair.Length - 3).ToLowerInvariant();
                var marketCurrency = tradingPair.Trim().ToLowerInvariant().Replace(baseCurrency, "").Replace("-", "").Replace("_", "");

                decimal amount = transaction[baseCurrency].ConvertInvariant<decimal>();
                decimal signedQuantity = transaction[marketCurrency].ConvertInvariant<decimal>();
                decimal quantity = Math.Abs(signedQuantity);
                decimal price = Math.Abs(amount / signedQuantity);
                bool isBuy = signedQuantity > 0;
                var id = transaction["id"].ToStringInvariant();
                var datetime = transaction["datetime"].ToDateTimeInvariant();
                var fee = transaction["fee"].ConvertInvariant<decimal>();
                var orderId = transaction["order_id"].ToStringInvariant();

                var bitstampTransaction = new BitstampTransaction(id, datetime, type, tradingPair, fee, orderId, quantity, price, isBuy);
                transactions.Add(bitstampTransaction);
            }

            return transactions;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/user_transactions/" : "/user_transactions/" + symbol;
            string url = "/user_transactions/";
            var payload = await GetNoncePayloadAsync();
            payload["limit"] = 1000;
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, payload, "POST");
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            foreach (var transaction in result as JArray)
            {
                int type = transaction["type"].ConvertInvariant<int>();
                // only type 2 is order transaction type, so we discard all other transactions
                if (type != 2) { continue; }

                string tradingPair = ((JObject)transaction).Properties().FirstOrDefault(p =>
                    !p.Name.Equals("order_id", StringComparison.InvariantCultureIgnoreCase)
                    && p.Name.Contains("_"))?.Name.Replace("_", "-");
                if (!string.IsNullOrWhiteSpace(tradingPair) && !string.IsNullOrWhiteSpace(marketSymbol) && !NormalizeMarketSymbol(tradingPair).Equals(marketSymbol))
                {
                    continue;
                }

                var quoteCurrency = tradingPair.Trim().Substring(tradingPair.Length - 3).ToLowerInvariant();
                var baseCurrency = tradingPair.Trim().ToLowerInvariant().Replace(quoteCurrency, "").Replace("-", "").Replace("_", "");

                decimal resultBaseCurrency = transaction[baseCurrency].ConvertInvariant<decimal>();
                ExchangeOrderResult order = new ExchangeOrderResult()
                {
                    OrderId = transaction["order_id"].ToStringInvariant(),
                    IsBuy = resultBaseCurrency > 0,
                    Fees = transaction["fee"].ConvertInvariant<decimal>(),
                    FeesCurrency = quoteCurrency.ToStringUpperInvariant(),
                    MarketSymbol = NormalizeMarketSymbol(tradingPair),
                    OrderDate = transaction["datetime"].ToDateTimeInvariant(),
                    AmountFilled = Math.Abs(resultBaseCurrency),
                    AveragePrice = transaction[$"{baseCurrency}_{quoteCurrency}"].ConvertInvariant<decimal>()
                };
                orders.Add(order);
            }
            // at this point one transaction transformed into one order, we need to consolidate parts into order
            // group by order id
            var groupings = orders.GroupBy(o => o.OrderId);
            List<ExchangeOrderResult> orders2 = new List<ExchangeOrderResult>();
            foreach (var group in groupings)
            {
                decimal spentQuoteCurrency = group.Sum(o => o.AveragePrice * o.AmountFilled);
                ExchangeOrderResult order = group.First();
                order.AmountFilled = group.Sum(o => o.AmountFilled);
                order.AveragePrice = spentQuoteCurrency / order.AmountFilled;
                order.Price = order.AveragePrice;
                orders2.Add(order);
            }

            return orders2;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new APIException("OrderId is needed for canceling order");
            }
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["id"] = orderId;
            await MakeJsonRequestAsync<JToken>("/cancel_order/", null, payload, "POST");
        }

        /// <summary>
        /// Function to withdraw from Bitsamp exchange. At the moment only XRP is supported.
        /// </summary>
        /// <param name="withdrawalRequest"></param>
        /// <returns></returns>
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            string baseurl = null;
            string url;
            switch (withdrawalRequest.Currency)
            {
                case "BTC":
                    // use old API for Bitcoin withdraw
                    baseurl = "https://www.bitstamp.net/api/";
                    url = "/bitcoin_withdrawal/";
                    break;
                default:
                    // this will work for some currencies and fail for others, caller must be aware of the supported currencies
                    url = "/" + withdrawalRequest.Currency.ToLowerInvariant() + "_withdrawal/";
                    break;
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["address"] = withdrawalRequest.Address.ToStringInvariant();
            payload["amount"] = withdrawalRequest.Amount.ToStringInvariant();
            payload["destination_tag"] = withdrawalRequest.AddressTag.ToStringInvariant();

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, baseurl, payload, "POST");
            CheckJsonResponse(responseObject);
            return new ExchangeWithdrawalResponse
            {
                Id = responseObject["id"].ToStringInvariant(),
                Message = responseObject["message"].ToStringInvariant(),
                Success = responseObject["success"].ConvertInvariant<bool>()
            };
        }

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(null, messageCallback: async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["event"].ToStringInvariant() == "bts:error")
				{ // {{"event": "bts:error", "channel": "",
				  // "data": {"code": null, "message": "Bad subscription string." }	}}
					token = token["data"];
					Logger.Info(token["code"].ToStringInvariant() + " "
						+ token["message"].ToStringInvariant());
				}
				else if (token["event"].ToStringInvariant() == "trade")
				{
					//{{
					//	"data": {
					//	"microtimestamp": "1563418286809203",
					//	"amount": 0.141247,
					//	"buy_order_id": 3785916113,
					//	"sell_order_id": 3785915893,
					//	"amount_str": "0.14124700",
					//	"price_str": "9754.23",
					//	"timestamp": "1563418286",
					//	"price": 9754.23,
					//	"type": 0, // Trade type (0 - buy; 1 - sell).
					//	"id": 94160906
					//	},
					//	"event": "trade",
					//	"channel": "live_trades_btcusd"
					//}}
					string marketSymbol = token["channel"].ToStringInvariant().Split('_')[2];
					var trade = token["data"].ParseTradeBitstamp(amountKey: "amount", priceKey: "price",
							typeKey: "type", timestampKey: "microtimestamp",
							TimestampType.UnixMicroeconds, idKey: "id",
							typeKeyIsBuyValue: "0");
					await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
				}
				else if (token["event"].ToStringInvariant() == "bts:subscription_succeeded")
				{   // {{	"event": "bts:subscription_succeeded",
					//"channel": "live_trades_btcusd",
					//"data": { } }}
				}
			}, connectCallback: async (_socket) =>
			{
				//{
				//	"event": "bts:subscribe",
				//	"data": {
				//		"channel": "[channel_name]"
				//	}
				//}
				foreach (var marketSymbol in marketSymbols)
				{
					await _socket.SendMessageAsync(new
					{
						@event = "bts:subscribe",
						data = new { channel = $"live_trades_{marketSymbol}" }
					});
				}
			});
		}
	}

    public partial class ExchangeName { public const string Bitstamp = "Bitstamp"; }
}
