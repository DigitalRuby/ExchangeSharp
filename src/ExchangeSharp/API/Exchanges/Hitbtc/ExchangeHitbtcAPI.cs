/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeHitBTCAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.hitbtc.com/api/2";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.hitbtc.com/api/2/ws";

		public ExchangeHitBTCAPI()
		{ // https://api.hitbtc.com/
			RateLimit = new RateGate(100, TimeSpan.FromSeconds(1));
			RequestContentType = "application/json";
			NonceStyle = NonceStyle.UnixMillisecondsString;
			MarketSymbolSeparator = string.Empty;
		}

		public override string PeriodSecondsToString(int seconds)
        {
            switch (seconds)
            {
                case 60: return "M1";
                case 180: return "M3";
                case 300: return "M5";
                case 900: return "M15";
                case 1800: return "M30";
                case 3600: return "H1";
                case 14400: return "H4";
                case 86400: return "D1";
                case 604800: return "D7";
				case 2419200: return "1M"; // 28 days
				case 2592000: return "1M"; // 30 days
				case 2678000: return "1M"; // 31 days
				case 4233600: return "1M"; // 49 days
				default: throw new ArgumentException(
					$"{nameof(seconds)} must be 60, 180, 300, 900, 1800, 3600, 14400, 86400, 604800, 2419200, 2592000, 2678000, 4233600");
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (method != "PUT" && method != "POST" && payload != null && payload.Count != 0)
            {
                url.AppendPayloadToQuery(payload);
            }
            return url.Uri;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            // only authenticated requests write json, everything uses GET and url params
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("Authorization", CryptoUtility.BasicAuthenticationString(PublicApiKey.ToUnsecureString(), PrivateApiKey.ToUnsecureString()));
                if (request.Method == "POST")
                {
                    await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
                }
            }
        }

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            //[{"id": "BTC", "fullName": "Bitcoin", "crypto": true, "payinEnabled": true, "payinPaymentId": false, "payinConfirmations": 2, "payoutEnabled": true, "payoutIsPaymentId": false, "transferEnabled": true, "delisted": false, "payoutFee": "0.00958" }, ...
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/currency");
            foreach (JToken token in obj)
            {
                bool enabled = token["delisted"].ToStringInvariant().Equals("false");
                currencies.Add(token["id"].ToStringInvariant(), new ExchangeCurrency()
                {
                    Name = token["id"].ToStringInvariant(),
                    FullName = token["fullName"].ToStringInvariant(),
                    TxFee = token["payoutFee"].ConvertInvariant<decimal>(),
                    MinConfirmations = token["payinConfirmations"].ConvertInvariant<int>(),
                    DepositEnabled = enabled,
                    WithdrawalEnabled = enabled
                });
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            // [ {"id": "ETHBTC","baseCurrency": "ETH","quoteCurrency": "BTC", "quantityIncrement": "0.001", "tickSize": "0.000001", "takeLiquidityRate": "0.001", "provideLiquidityRate": "-0.0001", "feeCurrency": "BTC"  } ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/symbol");
            foreach (JToken token in obj) symbols.Add(token["id"].ToStringInvariant());
            return symbols;
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // [ {"id": "ETHBTC","baseCurrency": "ETH","quoteCurrency": "BTC", "quantityIncrement": "0.001", "tickSize": "0.000001", "takeLiquidityRate": "0.001", "provideLiquidityRate": "-0.0001", "feeCurrency": "BTC"  } ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/symbol");
            foreach (JToken token in obj)
            {
                markets.Add(new ExchangeMarket()
                {
                    MarketSymbol = token["id"].ToStringInvariant(),
                    BaseCurrency = token["baseCurrency"].ToStringInvariant(),
                    QuoteCurrency = token["quoteCurrency"].ToStringInvariant(),
                    QuantityStepSize = token["quantityIncrement"].ConvertInvariant<decimal>(),
                    PriceStepSize = token["tickSize"].ConvertInvariant<decimal>(),
                    IsActive = true
                });
            }
            return markets;
        }


        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/ticker/" + marketSymbol);
            return await ParseTickerAsync(obj, marketSymbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/ticker");
            foreach (JToken token in obj)
            {
                string marketSymbol = NormalizeMarketSymbol(token["symbol"].ToStringInvariant());
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, await ParseTickerAsync(token, marketSymbol)));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            // [ {"timestamp": "2017-10-20T20:00:00.000Z","open": "0.050459","close": "0.050087","min": "0.050000","max": "0.050511","volume": "1326.628", "volumeQuote": "66.555987736"}, ... ]
            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            limit = limit ?? 100;
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/candles/" + marketSymbol + "?period=" + periodString + "&limit=" + limit);
            foreach (JToken token in obj)
            {
                candles.Add(this.ParseCandle(token, marketSymbol, periodSeconds, "open", "max", "min", "close", "timestamp", TimestampType.Iso8601, "volume", "volumeQuote"));
            }
            return candles;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
			// Putting an arbitrary limit of 10 for 'recent'
			// UPDATE: Putting an arbitrary limit of 100 for 'recent'

			//var maxRequestLimit = 1000; //hard coded for now, should add limit as an argument
			var maxRequestLimit = (limit == null || limit < 1 || limit > 1000) ? 1000 : (int)limit;

			JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + marketSymbol + "?limit=" + maxRequestLimit + "?sort=DESC");
			if(obj.HasValues) { //
				foreach(JToken token in obj) {
					trades.Add(ParseExchangeTrade(token));
				}
			}
            return trades;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/public/orderbook/" + marketSymbol + "?limit=" + maxCount.ToStringInvariant());
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(token, asks: "ask", bids: "bid", amount: "size", maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
			// TODO: Can't get Hitbtc to return other than the last 50 trades even though their API says it should (by orderid or timestamp). When passing either of these parms, it still returns the last 50
			// So until there is an update, that's what we'll go with
			// UPDATE: 2020/01/19 https://api.hitbtc.com/ GET /api/2/public/trades/{symbol} limit default: 100 max value:1000
			// 
			//var maxRequestLimit = 1000; //hard coded for now, should add limit as an argument
			var maxRequestLimit = (limit == null || limit < 1 || limit > 1000) ? 1000 : (int)limit;  
            //note that sort must come after limit, else returns default 100 trades, sort default is DESC
			JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + marketSymbol + "?limit=" + maxRequestLimit + "?sort=DESC"); 
			//JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + marketSymbol);
			if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    ExchangeTrade trade = ParseExchangeTrade(token);
                    if (startDate == null || trade.Timestamp >= startDate)
                    {
                        trades.Add(trade);
                    }
                }
                if (trades.Count != 0)
                {
					callback(trades); //no need to OrderBy or OrderByDescending, handled by sort=DESC or sort=ASC
                    //callback(trades.OrderBy(t => t.Timestamp));
				}
            }
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [ {"currency": "BTC","available": "0.0504600","reserved": "0.0000000"}, ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/trading/balance", null, await GetNoncePayloadAsync());
            foreach (JToken token in obj)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>() + token["reserved"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [ {"currency": "BTC","available": "0.0504600","reserved": "0.0000000"}, ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/trading/balance", null, await GetNoncePayloadAsync());
            foreach (JToken token in obj)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        /// <summary>
        /// HitBtc differentiates between active orders and historical trades. They also have three diffrent OrderIds (the id, the orderId, and the ClientOrderId).
        /// When placing an order, we return the ClientOrderId, which is only in force for the duration of the order. Completed orders are given a different id.
        /// Therefore, this call returns an open order only. Do not use it to return an historical trade order.
        /// To retrieve an historical trade order by id, use the GetCompletedOrderDetails with the symbol parameter empty, then filter for desired Id.
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/history/order/" + orderId + "/trades", null, await GetNoncePayloadAsync());
            if (obj != null && obj.HasValues) return ParseCompletedOrder(obj);
            return null;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = await GetNoncePayloadAsync();
            if (!string.IsNullOrEmpty(marketSymbol))
            {
                payload["symbol"] = marketSymbol;
            }
            if (afterDate != null)
            {
                payload["from"] = afterDate;
            }
            JToken obj = await MakeJsonRequestAsync<JToken>("/history/trades", null, payload);
            if (obj != null && obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    orders.Add(ParseCompletedOrder(token));
                }
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = await GetNoncePayloadAsync();
            if (!string.IsNullOrEmpty(marketSymbol))
            {
                payload["symbol"] = marketSymbol;
            }
            JToken obj = await MakeJsonRequestAsync<JToken>("/order", null, payload);
            if (obj != null && obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    orders.Add(ParseOpenOrder(token));
                }
            }
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var payload = await GetNoncePayloadAsync();
            //payload["clientOrderId"] = "neuMedia" + payload["nonce"];     Currently letting hitbtc assign this, but may not be unique for more than 24 hours
            payload["quantity"] = order.Amount;
            payload["symbol"] = order.MarketSymbol;
            payload["side"] = order.IsBuy ? "buy" : "sell";
            payload["type"] = order.OrderType == OrderType.Limit ? "limit" : "market";
            if (order.OrderType == OrderType.Limit)
            {
                payload["price"] = order.Price;
                payload["timeInForce"] = "GTC";
            }
            order.ExtraParameters.CopyTo(payload);

            // { "id": 0,"clientOrderId": "d8574207d9e3b16a4a5511753eeef175","symbol": "ETHBTC","side": "sell","status": "new","type": "limit","timeInForce": "GTC","quantity": "0.063","price": "0.046016","cumQuantity": "0.000","createdAt": "2017-05-15T17:01:05.092Z","updatedAt": "2017-05-15T17:01:05.092Z"  }
            JToken token = await MakeJsonRequestAsync<JToken>("/order", null, payload, "POST");
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                OrderId = token["id"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant(),
                OrderDate = token["createdAt"].ToDateTimeInvariant(),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                AmountFilled = token["cumQuantity"].ConvertInvariant<decimal>(),
                Message = token["clientOrderId"].ToStringInvariant()
            };
            if (result.AmountFilled >= result.Amount)
            {
                result.Result = ExchangeAPIOrderResult.Filled;
            }
            else if (result.AmountFilled > 0m)
            {
                result.Result = ExchangeAPIOrderResult.FilledPartially;
            }
            else
            {
                result.Result = ExchangeAPIOrderResult.Pending;
            }

            ParseAveragePriceAndFeesFromFills(result, token["tradesReport"]);

            return result;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            // this call returns info about the success of the cancel. Sure would be nice have a return type on this method.
            JToken token = await MakeJsonRequestAsync<JToken>("/order/" + orderId, null, await GetNoncePayloadAsync(), "DELETE");
        }

        private void ParseAveragePriceAndFeesFromFills(ExchangeOrderResult result, JToken fillsToken)
        {
            decimal totalCost = 0;
            decimal totalQuantity = 0;

            if (fillsToken is JArray)
            {
                foreach (var fill in fillsToken)
                {
                    result.Fees += fill["fee"].ConvertInvariant<decimal>();

                    decimal price = fill["price"].ConvertInvariant<decimal>();
                    decimal quantity = fill["quantity"].ConvertInvariant<decimal>();
                    totalCost += price * quantity;
                    totalQuantity += quantity;
                }
            }

            result.AveragePrice = (totalQuantity == 0 ? 0 : totalCost / totalQuantity);
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            ExchangeDepositDetails deposit = new ExchangeDepositDetails() { Currency = currency };
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/address/" + currency, null, await GetNoncePayloadAsync());
            if (token != null)
            {
                deposit.Address = token["address"].ToStringInvariant();
                if (deposit.Address.StartsWith("bitcoincash:"))
                {
                    deposit.Address = deposit.Address.Replace("bitcoincash:", string.Empty);  // don't know why they do this for bitcoincash
                }
                deposit.AddressTag = token["wallet"].ToStringInvariant();
            }
            return deposit;
        }


        /// <summary>
        /// This returns both Deposit and Withdawl history for the Bank and Trading Accounts. Currently returning everything and not filtering.
        /// There is no support for retrieving by Symbol, so we'll filter that after reteiving all symbols
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string? currency)
        {
            List<ExchangeTransaction> transactions = new List<ExchangeTransaction>();
            // [ {"id": "6a2fb54d-7466-490c-b3a6-95d8c882f7f7","index": 20400458,"currency": "ETH","amount": "38.616700000000000000000000","fee": "0.000880000000000000000000", "address": "0xfaEF4bE10dDF50B68c220c9ab19381e20B8EEB2B", "hash": "eece4c17994798939cea9f6a72ee12faa55a7ce44860cfb95c7ed71c89522fe8","status": "pending","type": "payout", "createdAt": "2017-05-18T18:05:36.957Z", "updatedAt": "2017-05-18T19:21:05.370Z" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/account/transactions", null, await GetNoncePayloadAsync());
            if (result != null && result.HasValues)
            {
                foreach (JToken token in result)
                {
                    if (string.IsNullOrWhiteSpace(currency) || token["currency"].ToStringInvariant().Equals(currency))
                    {
                        ExchangeTransaction transaction = new ExchangeTransaction
                        {
                            PaymentId = token["id"].ToStringInvariant(),
                            Currency = token["currency"].ToStringInvariant(),
                            Address = token["address"].ToStringInvariant(),               // Address Tag isn't returned
                            BlockchainTxId = token["hash"].ToStringInvariant(),           // not sure about this
                            Amount = token["amount"].ConvertInvariant<decimal>(),
                            Notes = token["type"].ToStringInvariant(),                    // since no notes are returned, we'll use this to show the transaction type
                            TxFee = token["fee"].ConvertInvariant<decimal>(),
                            Timestamp = token["createdAt"].ToDateTimeInvariant()
                        };

                        string status = token["status"].ToStringInvariant();
                        if (status.Equals("pending")) transaction.Status = TransactionStatus.Processing;
                        else if (status.Equals("success")) transaction.Status = TransactionStatus.Complete;
                        else if (status.Equals("failed")) transaction.Status = TransactionStatus.Failure;
                        else transaction.Status = TransactionStatus.Unknown;

                        transactions.Add(transaction);
                    }
                }
            }
            return transactions;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse withdraw = new ExchangeWithdrawalResponse() { Success = false };
            var payload = await GetNoncePayloadAsync();
            payload["amount"] = withdrawalRequest.Amount;
            payload["currency_code"] = withdrawalRequest.Currency;
            payload["address"] = withdrawalRequest.Address;
            if (!string.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload["paymentId"] = withdrawalRequest.AddressTag;
            //{ "id": "d2ce578f-647d-4fa0-b1aa-4a27e5ee597b"}   that's all folks!
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/payout", null, payload, "POST");
            if (token != null && token["id"] != null)
            {
                withdraw.Success = true;
                withdraw.Id = token["id"].ToStringInvariant();
            }
            return withdraw;
        }

		#endregion

		#region WebSocket APIs

		// working on it. Hitbtc has extensive support for sockets, including trading

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(null, messageCallback: async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["error"] != null)
				{   /* {
						  "jsonrpc": "2.0",
						  "error": {
							"code": 2001,
							"message": "Symbol not found",
							"description": "Try get /api/2/public/symbol, to get list of all available symbols."
						  },
						  "id": 123
						} */
					Logger.Info(token["error"]["code"].ToStringInvariant() + ", "
							+ token["error"]["message"].ToStringInvariant() + ", "
							+ token["error"]["description"].ToStringInvariant());
				}
				else if (token["method"].ToStringInvariant() == "snapshotTrades")
				{   /* snapshot: {
						  "jsonrpc": "2.0",
						  "method": "snapshotTrades",
						  "params": {
							"data": [
							  {
								"id": 54469456,
								"price": "0.054656",
								"quantity": "0.057",
								"side": "buy",
								"timestamp": "2017-10-19T16:33:42.821Z"
							  },
							  {
								"id": 54469497,
								"price": "0.054656",
								"quantity": "0.092",
								"side": "buy",
								"timestamp": "2017-10-19T16:33:48.754Z"
							  },
							  {
								"id": 54469697,
								"price": "0.054669",
								"quantity": "0.002",
								"side": "buy",
								"timestamp": "2017-10-19T16:34:13.288Z"
							  }
							],
							"symbol": "ETHBTC"
						  }
						} */
					token = token["params"];
					string marketSymbol = token["symbol"].ToStringInvariant();
					foreach (var tradesToken in token["data"])
					{
						var trade = parseTrade(tradesToken);
						trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
						await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
					}
				}
				else if (token["method"].ToStringInvariant() == "updateTrades")
				{   /* {
						  "jsonrpc": "2.0",
						  "method": "updateTrades",
						  "params": {
							"data": [
							  {
								"id": 54469813,
								"price": "0.054670",
								"quantity": "0.183",
								"side": "buy",
								"timestamp": "2017-10-19T16:34:25.041Z"
							  }
							],
							"symbol": "ETHBTC"
						  }
						}  */
					token = token["params"];
					string marketSymbol = token["symbol"].ToStringInvariant();
					foreach (var tradesToken in token["data"])
					{
						var trade = parseTrade(tradesToken);
						await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
					}
				}
			}, connectCallback: async (_socket) =>
			{   /* {
					  "method": "subscribeTrades",
					  "params": {
						"symbol": "ETHBTC",
						"limit": 100
					  },
					  "id": 123
					} */
					foreach (var marketSymbol in marketSymbols)
				{
					await _socket.SendMessageAsync(new
					{
						method = "subscribeTrades",
						@params = new {
								   symbol = marketSymbol,
								   limit = 10,
							   },
						id = CryptoUtility.UtcNow.Ticks // just need a unique number for client ID
					});
				}
			});
			ExchangeTrade parseTrade(JToken token) => token.ParseTrade(amountKey: "quantity",
				priceKey: "price", typeKey: "side", timestampKey: "timestamp",
				timestampType: TimestampType.Iso8601, idKey: "id");
		}

		#endregion

		#region Hitbtc Public Functions outside the ExchangeAPI
		// HitBTC has two accounts per client: the main bank and trading
		// Coins deposited from this API go into the bank, and must be withdrawn from there as well
		// Trading only takes place from the trading account.
		// You must transfer coin balances from the bank to trading in order to trade, and back again to withdaw
		// These functions aid in that process

		public async Task<Dictionary<string, decimal>> GetBankAmountsAsync()
		{
			Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
			JToken obj = await MakeJsonRequestAsync<JToken>("/account/balance", null, await GetNoncePayloadAsync());
			foreach (JToken token in obj)

			{
				decimal amount = token["available"].ConvertInvariant<decimal>();
				if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
			}
			return amounts;
		}

		public async Task<bool> AccountTransfer(string Symbol, decimal Amount, bool ToBank)
		{
			var payload = await GetNoncePayloadAsync();
			payload["type"] = ToBank ? "exchangeToBank" : "bankToExchange";
			payload["currency"] = Symbol;
			payload["amount"] = Amount;
			JToken obj = await MakeJsonRequestAsync<JToken>("/account/transfer", null, payload, "POST");
			return (obj != null && obj.HasValues && !String.IsNullOrEmpty(obj["id"].ToStringInvariant()));
		}

		#endregion

		#region Private Functions

		private async Task<ExchangeTicker> ParseTickerAsync(JToken token, string symbol)
        {
            // [ {"ask": "0.050043","bid": "0.050042","last": "0.050042","open": "0.047800","low": "0.047052","high": "0.051679","volume": "36456.720","volumeQuote": "1782.625000","timestamp": "2017-05-12T14:57:19.999Z","symbol": "ETHBTC"} ]
            return await this.ParseTickerAsync(token, symbol, "ask", "bid", "last", "volume", "volumeQuote", "timestamp", TimestampType.Iso8601);
        }

        private ExchangeTrade ParseExchangeTrade(JToken token)
        {
            // [ { "id": 9533117, "price": "0.046001", "quantity": "0.220", "side": "sell", "timestamp": "2017-04-14T12:18:40.426Z" }, ... ]
            return token.ParseTrade("quantity", "price", "side", "timestamp", TimestampType.Iso8601, "id");
        }

        private ExchangeOrderResult ParseCompletedOrder(JToken token)
        {
            //[ { "id": 9535486, "clientOrderId": "f8dbaab336d44d5ba3ff578098a68454", "orderId": 816088377, "symbol": "ETHBTC", "side": "sell", "quantity": "0.061", "price": "0.045487", "fee": "0.000002775", "timestamp": "2017-05-17T12:32:57.848Z" },
            return new ExchangeOrderResult()
            {
                OrderId = token["orderId"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                AmountFilled = token["quantity"].ConvertInvariant<decimal>(), // these are closed, so I guess the filled quantity matches the order quantiity
                Price = token["price"].ConvertInvariant<decimal>(),
                Fees = token["fee"].ConvertInvariant<decimal>(),
                OrderDate = token["timestamp"].ToDateTimeInvariant(),
                Result = ExchangeAPIOrderResult.Filled
            };
        }

        private ExchangeOrderResult ParseOpenOrder(JToken token)
        {
            // [ { "id": 840450210, "clientOrderId": "c1837634ef81472a9cd13c81e7b91401", "symbol": "ETHBTC", "side": "buy", "status": "partiallyFilled", "type": "limit", "timeInForce": "GTC", "quantity": "0.020", "price": "0.046001", "cumQuantity": "0.005", "createdAt": "2017-05-12T17:17:57.437Z",   "updatedAt": "2017-05-12T17:18:08.610Z" }]
            ExchangeOrderResult result = new ExchangeOrderResult()
            {
                OrderId = token["clientOrderId"].ToStringInvariant(),        // here we're using ClientOrderId in order to get order details by open orders
                MarketSymbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                AmountFilled = token["cumQuantity"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                OrderDate = token["createdAt"].ToDateTimeInvariant(),
                Message = string.Format("OrderType: {0}, TimeInForce: {1}", token["type"].ToStringInvariant(), token["timeInForce"].ToStringInvariant())   // A bit arbitrary, but this will show the ordertype and timeinforce
            };
            // new, suspended, partiallyFilled, filled, canceled, expired
            string status = token["status"].ToStringInvariant();
            switch (status)
            {
                case "filled": result.Result = ExchangeAPIOrderResult.Filled; break;
                case "partiallyFilled": result.Result = ExchangeAPIOrderResult.FilledPartially; break;
                case "canceled":
                case "expired": result.Result = ExchangeAPIOrderResult.Canceled; break;
                case "new": result.Result = ExchangeAPIOrderResult.Pending; break;
                default: result.Result = ExchangeAPIOrderResult.Error; break;
            }
            return result;
        }

        #endregion
    }

    public partial class ExchangeName { public const string HitBTC = "HitBTC"; }
}
