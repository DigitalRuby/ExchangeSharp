/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.Kraken.Models.Request;
using ExchangeSharp.API.Exchanges.Kraken.Models.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeKuCoinAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://openapi-v2.kucoin.com/api/v1";
		public override string BaseUrlWebSocket { get; set; } = "wss://push1.kucoin.com/endpoint";

		private ExchangeKuCoinAPI()
		{
			RequestContentType = "application/json";
			NonceStyle = NonceStyle.UnixMilliseconds;
			NonceEndPoint = "/timestamp";
			NonceEndPointField = "data";
			NonceEndPointStyle = NonceStyle.UnixMilliseconds;
			MarketSymbolSeparator = "-";
			RateLimit = new RateGate(60, TimeSpan.FromSeconds(1)); // https://www.kucoin.com/docs/basic-info/request-rate-limit/rest-api
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
		}

		public override string PeriodSecondsToString(int seconds) => CryptoUtility.SecondsToPeriodStringLong(seconds);

		protected override JToken CheckJsonResponse(JToken result)
		{
			if (result == null)
			{
				throw new APIException("No result from server");
			}

			if (!string.IsNullOrWhiteSpace(result["msg"].ToStringInvariant()))
			{
				throw new APIException(result.ToStringInvariant());
			}

			return base.CheckJsonResponse(result);
		}

		#region ProcessRequest

		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				request.AddHeader("KC-API-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("KC-API-TIMESTAMP", payload["nonce"].ToStringInvariant());
				request.AddHeader(
						"KC-API-PASSPHRASE",
						CryptoUtility.SHA256Sign(
								Passphrase.ToUnsecureString(),
								PrivateApiKey.ToUnsecureString(),
								true
						)
				);
				var endpoint = request.RequestUri.PathAndQuery;
				//For Gets, Deletes, no need to add the parameters in JSON format
				var message = "";
				var sig = "";
				if (request.Method == "GET" || request.Method == "DELETE")
				{
					//Request will be a querystring
					message = string.Format(
							"{0}{1}{2}",
							payload["nonce"],
							request.Method,
							endpoint
					);
					sig = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString(), true);
				}
				else if (request.Method == "POST")
				{
					message = string.Format(
							"{0}{1}{2}{3}",
							payload["nonce"],
							request.Method,
							endpoint,
							CryptoUtility.GetJsonForPayload(payload, true)
					);
					sig = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString(), true);
				}
				request.AddHeader("KC-API-KEY-VERSION", 2.ToStringInvariant());
				request.AddHeader("KC-API-SIGN", sig);
			}

			if (request.Method == "POST")
			{
				string msg = CryptoUtility.GetJsonForPayload(payload, true);
				byte[] content = msg.ToBytesUTF8();
				await request.WriteAllAsync(content, 0, content.Length);
			}
		}

		#endregion ProcessRequest

		#region Public APIs

		protected override async Task<
				IReadOnlyDictionary<string, ExchangeCurrency>
		> OnGetCurrenciesAsync()
		{
			/**
			 {
				"code": "200000",
				"data": [
					{
						"currency": "BTC",
						"name": "BTC",
						"fullName": "Bitcoin",
						"precision": 8,
						"confirms": null,
						"contractAddress": null,
						"isMarginEnabled": true,
						"isDebitEnabled": true,
						"chains": [
							{
								"chainName" : "BTC",
								"withdrawalMinFee" : "0.001",
								"withdrawalMinSize" : "0.0012",
								"withdrawFeeRate" : "0",
								"depositMinSize" : "0.0002",
								"isWithdrawEnabled" : true,
								"isDepositEnabled" : true,
								"preConfirms" : 1,
								"contractAddress" : "",
								"chainId" : "btc",
								"confirms" : 3
							},
							{
								"chainName" : "KCC",
								"withdrawalMinFee" : "0.00002",
								"withdrawalMinSize" : "0.0008",
								"withdrawFeeRate" : "0",
								"depositMinSize" : null,
								"isWithdrawEnabled" : true,
								"isDepositEnabled" : true,
								"preConfirms" : 20,
								"contractAddress" : "0xfa93c12cd345c658bc4644d1d4e1b9615952258c",
								"chainId" : "kcc",
								"confirms" : 20
							},
							{
								"chainName" : "BTC-Segwit",
								"withdrawalMinFee" : "0.0005",
								"withdrawalMinSize" : "0.0008",
								"withdrawFeeRate" : "0",
								"depositMinSize" : "0.0002",
								"isWithdrawEnabled" : false,
								"isDepositEnabled" : true,
								"preConfirms" : 2,
								"contractAddress" : "",
								"chainId" : "bech32",
								"confirms" : 2
							}
						]
					}
				]
			}
			*/

			Dictionary<string, ExchangeCurrency> currencies =
					new Dictionary<string, ExchangeCurrency>();
			List<string> symbols = new List<string>();
			JToken token = await MakeJsonRequestAsync<JToken>("/currencies");
			foreach (JToken currency in token)
				currencies.Add(
						currency["currency"].ToStringInvariant(),
						new ExchangeCurrency()
						{
							Name = currency["currency"].ToStringInvariant(),
							FullName = currency["fullName"].ToStringInvariant(),
							WithdrawalEnabled = currency["isDepositEnabled"].ConvertInvariant<bool>(),
							DepositEnabled = currency["isWithdrawEnabled"].ConvertInvariant<bool>(),
							TxFee = currency["withdrawalMinFee"].ConvertInvariant<decimal>(),
							MinConfirmations = currency["confirms"].ConvertInvariant<int>(),
							MinWithdrawalSize = currency[
										"withdrawalMinSize"
								].ConvertInvariant<decimal>(),
						}
				);
			return currencies;
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			List<string> symbols = new List<string>();
			// [{"symbol":"REQ-ETH","quoteMaxSize":"99999999","enableTrading":true,"priceIncrement":"0.0000001","baseMaxSize":"1000000","baseCurrency":"REQ","quoteCurrency":"ETH","market":"ETH","quoteIncrement":"0.0000001","baseMinSize":"1","quoteMinSize":"0.00001","name":"REQ-ETH","baseIncrement":"0.0001"}, ... ]
			JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/symbols");
			foreach (JToken marketSymbolToken in marketSymbolTokens)
				symbols.Add(marketSymbolToken["symbol"].ToStringInvariant());
			return symbols;
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			List<ExchangeMarket> markets = new List<ExchangeMarket>();
			/* [        {
			"symbol": "XWG-USDT",
            "name": "XWG-USDT",
            "baseCurrency": "XWG",
            "quoteCurrency": "USDT",
            "feeCurrency": "USDT",
            "market": "USDS",
            "baseMinSize": "10",
            "quoteMinSize": "0.1",
            "baseMaxSize": "10000000000",
            "quoteMaxSize": "99999999",
            "baseIncrement": "0.0001",
            "quoteIncrement": "0.0000001",
            "priceIncrement": "0.0000001",
            "priceLimitRate": "0.1",
            "minFunds": "0.1",
            "isMarginEnabled": false,
            "enableTrading": true,
            "st": true,
            "callauctionIsEnabled": false,
            "callauctionPriceFloor": null,
            "callauctionPriceCeiling": null,
            "callauctionFirstStageStartTime": null,
            "callauctionSecondStageStartTime": null,
            "callauctionThirdStageStartTime": null,
            "tradingStartTime": 1650531600000
				}, ... ] */
			JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/symbols");
			foreach (JToken marketSymbolToken in marketSymbolTokens)
			{
				ExchangeMarket market = new ExchangeMarket()
				{
					MarketSymbol = marketSymbolToken["symbol"].ToStringInvariant(),
					BaseCurrency = marketSymbolToken["baseCurrency"].ToStringInvariant(),
					QuoteCurrency = marketSymbolToken["quoteCurrency"].ToStringInvariant(),
					MinTradeSize = marketSymbolToken["baseMinSize"].ConvertInvariant<decimal>(),
					MinTradeSizeInQuoteCurrency = marketSymbolToken[
								"quoteMinSize"
						].ConvertInvariant<decimal>(),
					MaxTradeSize = marketSymbolToken["baseMaxSize"].ConvertInvariant<decimal>(),
					MaxTradeSizeInQuoteCurrency = marketSymbolToken[
								"quoteMaxSize"
						].ConvertInvariant<decimal>(),
					QuantityStepSize = marketSymbolToken[
								"baseIncrement"
						].ConvertInvariant<decimal>(),
					PriceStepSize = marketSymbolToken["priceIncrement"].ConvertInvariant<decimal>(),
					IsActive = marketSymbolToken["enableTrading"].ConvertInvariant<bool>(),
					IsDelistingCandidate = marketSymbolToken["st"].ConvertInvariant<bool>(),
				};
				markets.Add(market);
			}
			return markets;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/market/orderbook/level2_" + maxCount + "?symbol=" + marketSymbol
			);
			var book = token.ParseOrderBookFromJTokenArrays(asks: "asks", bids: "bids", sequence: "sequence");
			book.MarketSymbol = marketSymbol;
			return book;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			//{ "code":"200000","data":{ "sequence":"1550467754497","bestAsk":"0.000277","size":"107.3627934","price":"0.000276","bestBidSize":"2062.7337015","time":1551735305135,"bestBid":"0.0002741","bestAskSize":"223.177"} }
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/market/orderbook/level1?symbol=" + marketSymbol
			);
			return await this.ParseTickerAsync(token, marketSymbol);
		}

		protected override async Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			List<KeyValuePair<string, ExchangeTicker>> tickers =
					new List<KeyValuePair<string, ExchangeTicker>>();
			//{{
			//"ticker": [
			//{
			//"symbol": "LOOM-BTC",
			//"high": "0.00000295",
			//"vol": "23885.7388",
			//"last": "0.00000292",
			//"low": "0.00000284",
			//"buy": "0.00000291",
			//"sell": "0.00000292",
			//"changePrice": "0",
			//"averagePrice": "0.00000292",
			//"changeRate": "0",
			//"volValue": "0.069025547588"
			//},
			//{
			//"symbol": "BCD-BTC",
			//"high": "0.00006503",
			//"vol": "370.12648309",
			//"last": "0.00006442",
			//"low": "0.00006273",
			//"buy": "0.00006389",
			//"sell": "0.0000645",
			//"changePrice": "0.00000049",
			//"averagePrice": "0.00006527",
			//"changeRate": "0.0076",
			//"volValue": "0.0236902261670466"
			//},
			JToken token = await MakeJsonRequestAsync<JToken>("/market/allTickers");
			foreach (JToken tick in token["ticker"])
			{
				string marketSymbol = tick["symbol"].ToStringInvariant();
				tickers.Add(
						new KeyValuePair<string, ExchangeTicker>(
								marketSymbol,
								await ParseTickersAsync(tick, marketSymbol)
						)
				);
			}
			return tickers;
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string marketSymbol,
				int? limit = null
		)
		{
			List<ExchangeTrade> trades = await fetchTradeHistory(
					marketSymbol: marketSymbol,
					startDate: null,
					limit: limit
			);
			return trades.AsEnumerable().Reverse(); //descending - ie from newest to oldest trades
		}

		protected override async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			if (endDate != null)
			{
				throw new APIException("KuCoin does not allow specifying endDate");
			}
			List<ExchangeTrade> trades = await fetchTradeHistory(
					marketSymbol: marketSymbol,
					startDate: startDate,
					limit: limit
			);
			callback?.Invoke(trades);
		}

		async Task<List<ExchangeTrade>> fetchTradeHistory(
				string marketSymbol,
				DateTime? startDate,
				int? limit
		)
		{
			if (limit != null && limit != 100)
			{
				throw new ArgumentException("limit is always 100 in KuCoin");
			}
			List<ExchangeTrade> trades = new List<ExchangeTrade>();
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/market/histories?symbol="
							+ marketSymbol
							+ (
									startDate == null
											? string.Empty
											: "&since=" + startDate.Value.UnixTimestampFromDateTimeMilliseconds()
							)
			);
			/* {[	{
							"sequence": "1568570510897",
							"side": "buy",
							"size": "0.0025824",
							"price": "168.48",
							"time": 1579661286138826064
							},
							{
							"sequence": "1568570510943",
							"side": "buy",
							"size": "0.009223",
							"price": "168.48",
							"time": 1579661286980037641
							}, ... ]} */
			foreach (JObject trade in token)
			{
				trades.Add(
						trade.ParseTrade(
								amountKey: "size",
								priceKey: "price",
								typeKey: "side",
								timestampKey: "time",
								timestampType: TimestampType.UnixNanoseconds,
								idKey: "sequence"
						)
				);
			}
			return trades;
		}

		/// <summary>
		/// This is a private call on Kucoin and therefore requires an API Key + API Secret. Calling this without authorization will cause an exception
		/// </summary>
		/// <param name="marketSymbol"></param>
		/// <param name="periodSeconds"></param>
		/// <param name="startDate"></param>
		/// <param name="endDate"></param>
		/// <param name="limit"></param>
		/// <returns></returns>
		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			List<MarketCandle> candles = new List<MarketCandle>();

			string periodString = PeriodSecondsToString(periodSeconds);
			var payload = new Dictionary<string, object>
						{
								{ "symbol", marketSymbol },
								{ "type", periodString }
            };

			if (startDate != null)
			{
				payload.Add("startAt", (long)startDate.Value.UnixTimestampFromDateTimeSeconds());
			}
			if (endDate != null)
			{
				payload.Add("endAt", (long)endDate.Value.UnixTimestampFromDateTimeSeconds());
			}

			// The results of this Kucoin API call are also a mess. 6 different arrays (c,t,v,h,l,o) with the index of each shared for the candle values
			// It doesn't use their standard error format...
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/market/candles?" + payload.GetFormForPayload(false),
					null,
					payload
			);
			if (token != null && token.HasValues && token[0].ToStringInvariant() != null)
			{
				int childCount = token.Count();
				for (int i = 0; i < childCount; i++)
				{
					candles.Add(
							new MarketCandle
							{
								ExchangeName = this.Name,
								Name = marketSymbol,
								PeriodSeconds = periodSeconds,
								Timestamp = DateTimeOffset
											.FromUnixTimeSeconds(token[i][0].ConvertInvariant<long>())
											.DateTime,
								OpenPrice = token[i][1].ConvertInvariant<decimal>(),
								ClosePrice = token[i][2].ConvertInvariant<decimal>(),
								HighPrice = token[i][3].ConvertInvariant<decimal>(),
								LowPrice = token[i][4].ConvertInvariant<decimal>(),
								BaseCurrencyVolume = token[i][5].ConvertInvariant<decimal>(),
								QuoteCurrencyVolume = token[i][6].ConvertInvariant<decimal>()
							}
					);
				}
			}
			return candles;
		}

		#endregion Public APIs

		#region Private APIs

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			return await OnGetAmountsInternalAsync(true);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			return await OnGetAmountsInternalAsync(false);
		}

		protected override async Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			var payload = await GetNoncePayloadAsync();
			payload["status"] = "done";
			if (string.IsNullOrWhiteSpace(marketSymbol)) { }
			else
			{
				payload["symbol"] = marketSymbol;
			}

			JToken token = await MakeJsonRequestAsync<JToken>(
					"/orders?" + CryptoUtility.GetFormForPayload(payload, false, false),
					null,
					payload
			);
			if (token != null && token.HasValues)
			{
				foreach (JToken order in token["items"])
				{
					orders.Add(ParseCompletedOrder(order));
				}
			}
			return orders;
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			// { "SELL": [{ "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 } ... ],
			//   "BUY":  [{ "oid": "59e42bf09bd8d374c9956caa", "type": "BUY",  "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "price": 0.00009727,"dealAmount": 31.14503, "pendingAmount": 16.94827, "createdAt": 1508125681000, "updatedAt": 1508125681000 } ... ]
			var payload = await GetNoncePayloadAsync();
			payload["status"] = "active";
			if (marketSymbol != null && marketSymbol != "")
			{
				payload["symbol"] = marketSymbol;
			}

			JToken token = await MakeJsonRequestAsync<JToken>(
					"/orders?&" + CryptoUtility.GetFormForPayload(payload, false),
					null,
					payload
			);
			if (token != null && token.HasValues)
			{
				foreach (JToken order in token["items"])
				{
					orders.Add(ParseOpenOrder(order));
				}
			}
			return orders;
		}

		/// <summary>
		///  Get Order by ID or ClientOrderId
		/// </summary>
		/// <param name="orderId"></param>
		/// <returns></returns>
		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var payload = await GetNoncePayloadAsync();
			JToken token = await MakeJsonRequestAsync<JToken>(
					$"/orders{(isClientOrderId ? "/client-order" : null)}/{orderId}"
							+ CryptoUtility.GetFormForPayload(payload, false),
					null,
					payload
			);
			var isActive = token["isActive"].ToObject<bool>();
			if (isActive)
				return ParseOpenOrder(token);
			else
				return ParseCompletedOrder(token);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			var payload = await GetNoncePayloadAsync();
			payload["clientOid"] = Guid.NewGuid();
			payload["size"] = order.Amount;
			payload["symbol"] = order.MarketSymbol;
			payload["side"] = order.IsBuy ? "buy" : "sell";
			if (order.OrderType == OrderType.Market)
			{
				payload["type"] = "market";
			}
			else if (order.OrderType == OrderType.Limit)
			{
				payload["type"] = "limit";
				payload["price"] = order.Price.ToStringInvariant();
				if (order.IsPostOnly != null)
					payload["postOnly"] = order.IsPostOnly; // [Optional] Post only flag, invalid when timeInForce is IOC or FOK
			}
			order.ExtraParameters.CopyTo(payload);

			// {"orderOid": "596186ad07015679730ffa02" }
			JToken token = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");
			return new ExchangeOrderResult() { OrderId = token["orderId"].ToStringInvariant() }; // this is different than the oid created when filled
		}

		/// <summary>
		/// Must pass the Original Order ID returned from PlaceOrder, not the OrderID returned from GetOrder
		/// </summary>
		/// <param name="orderId">The Original Order Id return from Place Order</param>
		/// <returns></returns>
		protected override async Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var payload = await GetNoncePayloadAsync();

			JToken token = await MakeJsonRequestAsync<JToken>(
					isClientOrderId ? "/order/client-order/" : "/orders/" + orderId,
					null,
					payload,
					"DELETE"
			);
		}

		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(
				string currency,
				bool forceRegenerate = false
		)
		{
			// { "oid": "598aeb627da3355fa3e851ca", "address": "598aeb627da3355fa3e851ca", "context": null, "userOid": "5969ddc96732d54312eb960e", "coinType": "KCS", "createdAt": 1502276446000, "deletedAt": null, "updatedAt": 1502276446000,    "lastReceivedAt": 1502276446000   }
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/account/" + currency + "/wallet/address",
					null,
					await GetNoncePayloadAsync()
			);
			if (token != null && token.HasValues)
			{
				return new ExchangeDepositDetails()
				{
					Currency = currency,
					Address = token["address"].ToStringInvariant(),
					AddressTag = token["userOid"].ToStringInvariant() // this isn't in their documentation, but is how it's being used on other interfaces
				};
			}
			return null;
		}

		/// <summary>
		/// Kucoin doesn't support withdraws to Cryptonight currency addresses (No Address Tag paramater)
		/// </summary>
		/// <param name="withdrawalRequest"></param>
		/// <returns></returns>
		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest withdrawalRequest
		)
		{
			ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = true };
			var payload = await GetNoncePayloadAsync();
			payload["address"] = withdrawalRequest.Address;
			payload["amount"] = withdrawalRequest.Amount;

			JToken token = await MakeJsonRequestAsync<JToken>(
					"/account/" + withdrawalRequest.Currency + "/withdraw/apply",
					null,
					payload,
					"POST"
			);
			// no data is returned. Check error will throw exception on failure
			return response;
		}

		#endregion Private APIs

		#region Websockets

		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(
				Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback,
				params string[] marketSymbols
		)
		{
			var websocketUrlToken = GetWebsocketBulletToken();
			return await ConnectPublicWebSocketAsync(
					$"?token={websocketUrlToken}&acceptUserMessage=true",
					async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token["type"].ToStringInvariant() == "message")
						{
							var dataToken = token["data"];
							string marketSymbol;
							if (token["topic"].ToStringInvariant() == $"/market/ticker:all")
							{
								//{"data":{"sequence":"1612818290539","bestAsk":"0.387788","size":"304.072","bestBidSize":"654.3404","price":"0.387788","time":1618870257524,"bestAskSize":"0.00005226","bestBid":"0.386912"},"subject":"XEM-USDT","topic":"/market/ticker:all","type":"message"}
								marketSymbol = token["subject"].ToStringInvariant();
							}
							else
							{
								//{"data":{"sequence":"1615451293654","bestAsk":"55627.3","size":"0.00075576","bestBidSize":"0.0205","price":"55627.2","time":1618875110592,"bestAskSize":"0.24831204","bestBid":"55627.2"},"subject":"trade.ticker","topic":"/market/ticker:BTC-USDT","type":"message"}
								const string topicPrefix = "/market/ticker:";
								marketSymbol = token["topic"].ToStringInvariant();
								if (!marketSymbol.StartsWith(topicPrefix))
									return;
								marketSymbol = marketSymbol.Substring(topicPrefix.Length);
							}

							ExchangeTicker ticker = await this.ParseTickerAsync(
												dataToken,
												marketSymbol,
												"bestAsk",
												"bestBid",
												"price",
												"size",
												null,
												"time",
												TimestampType.UnixMilliseconds,
												idKey: "sequence"
										);
							callback(
												new List<KeyValuePair<string, ExchangeTicker>>()
											{
																new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker)
											}
										);
						}
					},
					async (_socket) =>
					{
						var id = CryptoUtility.UtcNow.Ticks;
						var topic =
											marketSymbols.Length == 0
													? $"/market/ticker:all"
													: $"/market/ticker:{string.Join(",", marketSymbols)}";
						await _socket.SendMessageAsync(
											new
											{
												id = id++,
												type = "subscribe",
												topic = topic
											}
									);
					}
			);
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			//{
			//  "id":"5c24c5da03aa673885cd67aa",
			//  "type":"message",
			//  "topic":"/market/match:BTC-USDT",
			//  "subject":"trade.l3match",
			//  "sn":1545896669145,
			//  "data":{
			//    "sequence":"1545896669145",
			//    "symbol":"BTC-USDT",
			//    "side":"buy",
			//    "size":"0.01022222000000000000",
			//    "price":"0.08200000000000000000",
			//    "takerOrderId":"5c24c5d903aa6772d55b371e",
			//    "time":"1545913818099033203",
			//    "type":"match",
			//    "makerOrderId":"5c2187d003aa677bd09d5c93",
			//    "tradeId":"5c24c5da03aa673885cd67aa"
			//  }
			//}
			var websocketUrlToken = GetWebsocketBulletToken();
			return await ConnectPublicWebSocketAsync(
					$"?token={websocketUrlToken}",
					async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token["type"].ToStringInvariant() == "message")
						{
							var dataToken = token["data"];
							var marketSymbol = token["data"]["symbol"].ToStringInvariant();
							var trade = dataToken.ParseTradeKucoin(
												amountKey: "size",
												priceKey: "price",
												typeKey: "side",
												timestampKey: "time",
												TimestampType.UnixNanoseconds,
												idKey: "tradeId"
										);
							await callback(
												new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
										);
						}
						else if (token["type"].ToStringInvariant() == "error")
						{
							Logger.Info(token["data"].ToStringInvariant());
						}
					},
					async (_socket) =>
					{
						List<string> marketSymbolsList = new List<string>(
											marketSymbols == null || marketSymbols.Length == 0
													? await GetMarketSymbolsAsync()
													: marketSymbols
									);
						StringBuilder symbolsSB = new StringBuilder();
						var id = CryptoUtility.UtcNow.Ticks; // just needs to be a "Unique string to mark the request"
						int tunnelInt = 0;
						while (marketSymbolsList.Count > 0)
						{ // can only subscribe to 100 symbols per session (started w/ API 2.0)
							var batchSize = marketSymbolsList.Count > 100 ? 100 : marketSymbolsList.Count;
							var nextBatch = marketSymbolsList.GetRange(index: 0, count: batchSize);
							marketSymbolsList.RemoveRange(index: 0, count: batchSize);
							// create a new tunnel
							await _socket.SendMessageAsync(
												new
												{
													id = id++,
													type = "openTunnel",
													newTunnelId = $"bt{tunnelInt}",
													response = "true",
												}
										);
							// wait for tunnel to be created
							await Task.Delay(millisecondsDelay: 1000);
							// subscribe to Match Execution Data
							await _socket.SendMessageAsync(
												new
												{
													id = id++,
													type = "subscribe",
													topic = $"/market/match:{string.Join(",", nextBatch)}",
													tunnelId = $"bt{tunnelInt}",
													privateChannel = "false", //Adopted the private channel or not. Set as false by default.
													response = "true",
												}
										);
							tunnelInt++;
						}
					}
			);
		}

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(
				Action<ExchangeOrderBook> callback,
				int maxCount = 20,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync(true)).ToArray();
			}

			var initialSequenceIds = new Dictionary<string, long>();

			foreach (var marketSymbol in marketSymbols)
			{
				var initialBook = await OnGetOrderBookAsync(marketSymbol, maxCount);
				initialBook.IsFromSnapshot = true;

				callback(initialBook);

				initialSequenceIds[marketSymbol] = initialBook.SequenceId;
			}

			var websocketUrlToken = GetWebsocketBulletToken();

			return await ConnectPublicWebSocketAsync(
					$"?token={websocketUrlToken}&acceptUserMessage=true",
					messageCallback: (_socket, msg) =>
					{
						var message = msg.ToStringFromUTF8();
						var deserializedMessage = JsonConvert.DeserializeObject(message) as JObject;

						var data = deserializedMessage["data"];
						if (data is null)
						{
							return Task.CompletedTask;
						}

						var changes = data["changes"];
						var time = data["time"];
						var symbol = data["symbol"];
						if (changes is null || time is null || symbol is null)
						{
							return Task.CompletedTask;
						}

						var parsedTime = time.ConvertInvariant<long>();
						var lastUpdatedDateTime = DateTimeOffset
							.FromUnixTimeMilliseconds(parsedTime)
							.DateTime;

						var deltaBook = new ExchangeOrderBook
						{
							IsFromSnapshot = false,
							ExchangeName = ExchangeName.KuCoin,
							SequenceId = parsedTime,
							MarketSymbol = symbol.ToString(),
							LastUpdatedUtc = lastUpdatedDateTime,
						};

						var rawAsks = changes["asks"] as JArray;
						foreach (var rawAsk in rawAsks)
						{
							var sequence = rawAsk[2].ConvertInvariant<long>();
							if (sequence <= initialSequenceIds[deltaBook.MarketSymbol])
							{
								// A deprecated update should be ignored
								continue;
							}

							var price = rawAsk[0].ConvertInvariant<decimal>();
							var quantity = rawAsk[1].ConvertInvariant<decimal>();

							deltaBook.Asks[price] = new ExchangeOrderPrice
							{
								Price = price,
								Amount = quantity,
							};
						}

						var rawBids = changes["bids"] as JArray;
						foreach (var rawBid in rawBids)
						{
							var sequence = rawBid[2].ConvertInvariant<long>();
							if (sequence <= initialSequenceIds[deltaBook.MarketSymbol])
							{
								// A deprecated update should be ignored
								continue;
							}

							var price = rawBid[0].ConvertInvariant<decimal>();
							var quantity = rawBid[1].ConvertInvariant<decimal>();

							deltaBook.Bids[price] = new ExchangeOrderPrice
							{
								Price = price,
								Amount = quantity,
							};
						}

						callback(deltaBook);

						return Task.CompletedTask;
					},
					connectCallback: async (_socket) =>
					{
						var marketSymbolsForSubscriptionString = string.Join(",", marketSymbols);

						var id = CryptoUtility.UtcNow.Ticks;
						var topic = $"/market/level2:{marketSymbolsForSubscriptionString}";
						await _socket.SendMessageAsync(
								new
								{
									id = id++,
									type = "subscribe",
									topic = topic,
								}
						);
					}
			);
		}

		#endregion Websockets

		#region Private Functions

		private async Task<ExchangeTicker> ParseTickerAsync(JToken token, string symbol)
		{
			//            //Get Ticker
			//            {
			//                "sequence": "1550467636704",
			//    "bestAsk": "0.03715004",
			//    "size": "0.17",
			//    "price": "0.03715005",
			//    "bestBidSize": "3.803",
			//    "bestBid": "0.03710768",
			//    "bestAskSize": "1.788",
			//    "time": 1550653727731

			//}
			return await this.ParseTickerAsync(
					token,
					symbol,
					"bestAsk",
					"bestBid",
					"price",
					"bestAskSize"
			);
		}

		private async Task<ExchangeTicker> ParseTickersAsync(JToken token, string symbol)
		{
			//      {
			//          "symbol": "LOOM-BTC",
			//  "buy": "0.00001191",
			//  "sell": "0.00001206",
			//  "changeRate": "0.057",
			//  "changePrice": "0.00000065",
			//  "high": "0.0000123",
			//  "low": "0.00001109",
			//  "vol": "45161.5073",
			//  "last": "0.00001204"
			//},
			return await this.ParseTickerAsync(token, symbol, "sell", "buy", "last", "vol");
		}

		// { "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 }
		private ExchangeOrderResult ParseOpenOrder(JToken token)
		{
			ExchangeOrderResult order = new ExchangeOrderResult()
			{
				OrderId = token["id"].ToStringInvariant(),
				MarketSymbol = token["symbol"].ToStringInvariant(),
				IsBuy = token["side"].ToStringInvariant().Equals("buy"), //changed to lower
				Price = token["price"].ConvertInvariant<decimal>(),
				AveragePrice = token["price"].ConvertInvariant<decimal>(),
				OrderDate = DateTimeOffset
							.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>())
							.DateTime
			};

			if (order.AveragePrice == 0)
			{
				order.AveragePrice = token["dealFunds"].ConvertInvariant<decimal>() / token["dealSize"].ConvertInvariant<decimal>();
			}

			// Amount and Filled are returned as Sold and Pending, so we'll adjust
			order.AmountFilled = token["dealSize"].ConvertInvariant<decimal>();
			order.Amount = token["size"].ConvertInvariant<decimal>() + order.AmountFilled.Value;

			if (order.Amount == order.AmountFilled)
				order.Result = ExchangeAPIOrderResult.Filled;
			else if (order.AmountFilled == 0m)
				order.Result = ExchangeAPIOrderResult.Open;
			else
				order.Result = ExchangeAPIOrderResult.FilledPartially;

			return order;
		}

		// {"createdAt": 1508219588000, "amount": 92.79323381, "dealValue": 0.00927932, "dealPrice": 0.0001, "fee": 1e-8,"feeRate": 0, "oid": "59e59ac49bd8d31d09f85fa8", "orderOid": "59e59ac39bd8d31d093d956a", "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "dealDirection": "BUY" }
		private ExchangeOrderResult ParseCompletedOrder(JToken token)
		{
			ExchangeOrderResult order = new ExchangeOrderResult()
			{
				OrderId = token["id"].ToStringInvariant(),
				MarketSymbol = token["symbol"].ToStringInvariant(),
				IsBuy = token["side"].ToStringInvariant().Equals("buy"), //changed to lower
				Amount = token["size"].ConvertInvariant<decimal>(),
				AmountFilled = token["dealSize"].ConvertInvariant<decimal>(),
				Price = token["price"].ConvertInvariant<decimal>(),
				AveragePrice = token["price"].ConvertInvariant<decimal>(),
				//Message = string.Format("Original Order ID: {0}", token["orderOid"].ToStringInvariant()),           // each new order is given an order ID. As it is filled, possibly across multipl orders, a new oid is created. Here we put the orginal orderid
				Fees = token["fee"].ConvertInvariant<decimal>(), // ConvertInvariant handles exponent now
				OrderDate = DateTimeOffset
							.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>())
							.DateTime
			};

			if (order.AveragePrice == 0)
			{
				order.AveragePrice = token["dealFunds"].ConvertInvariant<decimal>() / token["dealSize"].ConvertInvariant<decimal>();
			}

			if (token["cancelExist"].ToStringInvariant().ToUpper() == "TRUE")
			{
				order.Result = ExchangeAPIOrderResult.Canceled;
			}
			else
			{
				order.Result = ExchangeAPIOrderResult.Filled;
			}

			return order;
		}

		private async Task<Dictionary<string, decimal>> OnGetAmountsInternalAsync(
				bool includeFreezeBalance
		)
		{
			//            {
			//                "id": "5bd6e9216d99522a52e458d6",
			//    "currency": "BTC",
			//    "type": "trade",
			//    "balance": "1234356",
			//    "available": "1234356",
			//    "holds": "0"
			//}]

			///api/v1/accounts
			Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
			JToken obj = await MakeJsonRequestAsync<JToken>(
					"/accounts",
					payload: await GetNoncePayloadAsync()
			);
			foreach (var ob in obj)
			{
				if (ob["type"].ToStringInvariant().ToLower() == "trade")
				{
					amounts.Add(
							ob["currency"].ToStringInvariant(),
							ob[
									(includeFreezeBalance ? "balance" : "available")
							].ConvertInvariant<decimal>()
					);
				}
			}

			//amounts.Add(obj["currency"].ToStringInvariant(), obj["available"].ConvertInvariant<decimal>());
			//amounts.Add(obj["type"].ToStringInvariant(), obj["trade"].ToStringInvariant());
			return amounts;
		}

		private string GetWebsocketBulletToken()
		{
			Dictionary<string, object> payload = new Dictionary<string, object>()
			{
				["code"] = "200000",
				["data"] = new
				{
					instanceServers = new[]
							{
												new Dictionary<string, object>()
												{
														["pingInterval"] = 50000,
														["endpoint"] = "wss://push1-v2.kucoin.net/endpoint",
														["protocol"] = "websocket",
														["encrypt"] = "true",
														["pingTimeout"] = 10000,
												}
										}
				},
				//["token"] = "vYNlCtbz4XNJ1QncwWilJnBtmmfe4geLQDUA62kKJsDChc6I4bRDQc73JfIrlFaVYIAE0Gv2",
			};
			var jsonRequestTask = MakeJsonRequestAsync<JToken>(
					"/bullet-public",
					baseUrl: BaseUrl,
					payload: payload,
					requestMethod: "POST"
			);
			// wait for one second before timing out so we don't hold up the thread
			jsonRequestTask.Wait(TimeSpan.FromSeconds(1));
			var result = jsonRequestTask.Result;
			// in the future, they may introduce new server endpoints, possibly for load balancing
			this.BaseUrlWebSocket = result["instanceServers"][0]["endpoint"].ToStringInvariant();
			return result["token"].ToStringInvariant();
		}

		#endregion Private Functions
	}

	public partial class ExchangeName
	{
		public const string KuCoin = "KuCoin";
	}
}
