/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeGeminiAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.gemini.com/v1";
		public override string BaseUrlWebSocket { get; set; } =
				"wss://api.gemini.com/v2/marketdata";

		private ExchangeGeminiAPI()
		{
			MarketSymbolIsUppercase = false;
			MarketSymbolSeparator = string.Empty;
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
			RateLimit = new RateGate(1, TimeSpan.FromSeconds(0.5));
		}

		private async Task<ExchangeVolume> ParseVolumeAsync(JToken token, string symbol)
		{
			ExchangeVolume vol = new ExchangeVolume();
			JProperty[] props = token.Children<JProperty>().ToArray();
			if (props.Length == 3)
			{
				var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(
						symbol
				);
				vol.QuoteCurrency = quoteCurrency.ToUpperInvariant();
				vol.QuoteCurrencyVolume = token[
						quoteCurrency.ToUpperInvariant()
				].ConvertInvariant<decimal>();
				vol.BaseCurrency = baseCurrency.ToUpperInvariant();
				vol.BaseCurrencyVolume = token[
						baseCurrency.ToUpperInvariant()
				].ConvertInvariant<decimal>();
				vol.Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
						props[2].Value.ConvertInvariant<long>()
				);
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
				Result = (
							amountFilled == amount
									? ExchangeAPIOrderResult.Filled
									: (
											amountFilled == 0
													? ExchangeAPIOrderResult.Open
													: ExchangeAPIOrderResult.FilledPartially
									)
					),
				OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
							result["timestampms"].ConvertInvariant<double>()
					),
				MarketSymbol = result["symbol"].ToStringInvariant(),
				IsBuy = result["side"].ToStringInvariant() == "buy"
			};
		}

		protected override Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				payload.Add("request", request.RequestUri.AbsolutePath);
				string json = JsonConvert.SerializeObject(payload);
				string json64 = System.Convert.ToBase64String(json.ToBytesUTF8());
				string hexSha384 = CryptoUtility.SHA384Sign(
						json64,
						CryptoUtility.ToUnsecureString(PrivateApiKey)
				);
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

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			List<ExchangeMarket> markets = new List<ExchangeMarket>();

			try
			{
				string html = (
						await RequestMaker.MakeRequestAsync("/rest-api", "https://docs.gemini.com")
				).Response;
				int startPos = html.IndexOf(
						"<h1 id=\"symbols-and-minimums\">Symbols and minimums</h1>"
				);
				if (startPos < 0)
				{
					throw new ApplicationException(
							"Gemini html for symbol metadata is missing expected h1 tag and id"
					);
				}

				startPos = html.IndexOf("<tbody>", startPos);
				if (startPos < 0)
				{
					throw new ApplicationException(
							"Gemini html for symbol metadata is missing start tbody tag"
					);
				}

				int endPos = html.IndexOf("</tbody>", startPos);
				if (endPos < 0)
				{
					throw new ApplicationException(
							"Gemini html for symbol metadata is missing ending tbody tag"
					);
				}

				string table = html.Substring(startPos, endPos - startPos + "</tbody>".Length);
				string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n" + table;
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xml);
				if (doc.ChildNodes.Count < 2)
				{
					throw new ApplicationException(
							"Gemini html for symbol metadata does not have the expected number of nodes"
					);
				}

				XmlNode root = doc.ChildNodes.Item(1);
				foreach (XmlNode tr in root.ChildNodes)
				{
					// <tr>
					// <th>Symbol</th>
					// <th>Minimum Order Size</th>
					// <th>Tick Size</th>
					// <th>Quote Currency Price Increment</th>

					// <td>btcusd</td>
					// <td>0.00001 BTC (1e-5)</td>
					// <td>0.00000001 BTC (1e-8)</td>
					// <td>0.01 USD</td>
					// </tr>

					if (tr.ChildNodes.Count != 4)
					{
						throw new ApplicationException(
								"Gemini html for symbol metadata does not have 4 rows per entry anymore"
						);
					}

					ExchangeMarket market = new ExchangeMarket { IsActive = true };
					XmlNode symbolNode = tr.ChildNodes.Item(0);
					XmlNode minOrderSizeNode = tr.ChildNodes.Item(1);
					XmlNode tickSizeNode = tr.ChildNodes.Item(2);
					XmlNode incrementNode = tr.ChildNodes.Item(3);
					string symbol = symbolNode.InnerText;
					int minOrderSizePos = minOrderSizeNode.InnerText.IndexOf(' ');
					if (minOrderSizePos < 0)
					{
						throw new ArgumentException(
								"Min order size text does not have a space after the number"
						);
					}
					decimal minOrderSize = minOrderSizeNode.InnerText
							.Substring(0, minOrderSizePos)
							.ConvertInvariant<decimal>();
					int tickSizePos = tickSizeNode.InnerText.IndexOf(' ');
					if (tickSizePos < 0)
					{
						throw new ArgumentException(
								"Tick size text does not have a space after the number"
						);
					}
					decimal tickSize = tickSizeNode.InnerText
							.Substring(0, tickSizePos)
							.ConvertInvariant<decimal>();
					int incrementSizePos = incrementNode.InnerText.IndexOf(' ');
					if (incrementSizePos < 0)
					{
						throw new ArgumentException(
								"Increment size text does not have a space after the number"
						);
					}
					decimal incrementSize = incrementNode.InnerText
							.Substring(0, incrementSizePos)
							.ConvertInvariant<decimal>();
					market.MarketSymbol = symbol;
					market.AltMarketSymbol = symbol.ToUpper();
					market.BaseCurrency = symbol.Substring(0, symbol.Length - 3);
					market.QuoteCurrency = symbol.Substring(symbol.Length - 3);
					market.MinTradeSize = minOrderSize;
					market.QuantityStepSize = tickSize;
					market.PriceStepSize = incrementSize;
					markets.Add(market);
				}
				return markets;
			}
			catch (Exception ex)
			{
				markets.Clear();
				Logger.Error(
						ex,
						"Failed to parse gemini symbol metadata web page, falling back to per symbol query..."
				);
			}

			// slow way, fetch each symbol one by one, gemini api epic fail
			Logger.Warn("Fetching gemini symbol metadata per symbol, this may take a minute...");

			string[] symbols = (await GetMarketSymbolsAsync()).ToArray();
			List<Task> tasks = new List<Task>();
			foreach (string symbol in symbols)
			{
				tasks.Add(
						Task.Run(async () =>
						{
							JToken token = await MakeJsonRequestAsync<JToken>(
													"/symbols/details/" + HttpUtility.UrlEncode(symbol)
											);

							// {"symbol":"BTCUSD","base_currency":"BTC","quote_currency":"USD","tick_size":1E-8,"quote_increment":0.01,"min_order_size":"0.00001","status":"open"}
							lock (markets)
							{
								markets.Add(
														new ExchangeMarket
														{
															BaseCurrency = token["base_currency"].ToStringInvariant(),
															IsActive = token["status"]
																		.ToStringInvariant()
																		.Equals("open", StringComparison.OrdinalIgnoreCase),
															MarketSymbol = token["symbol"].ToStringInvariant(),
															MinTradeSize = token[
																		"min_order_size"
																].ConvertInvariant<decimal>(),
															QuantityStepSize = token[
																		"tick_size"
																].ConvertInvariant<decimal>(),
															QuoteCurrency = token["quote_currency"].ToStringInvariant(),
															PriceStepSize = token[
																		"quote_increment"
																].ConvertInvariant<decimal>()
														}
												);
							}
						})
				);
			}
			await Task.WhenAll(tasks);

			Logger.Warn("Gemini symbol metadata fetched and cached for several hours.");

			return markets;
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
				Exchange = Name,
				MarketSymbol = marketSymbol,
				ApiResponse = obj,
				Ask = obj["ask"].ConvertInvariant<decimal>(),
				Bid = obj["bid"].ConvertInvariant<decimal>(),
				Last = obj["last"].ConvertInvariant<decimal>()
			};
			t.Volume = await ParseVolumeAsync(obj["volume"], marketSymbol);
			return t;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			JToken obj = await MakeJsonRequestAsync<JToken>(
					"/book/" + marketSymbol + "?limit_bids=" + maxCount + "&limit_asks=" + maxCount
			);
			return obj.ParseOrderBookFromJTokenDictionaries();
		}

		protected override async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
			{
				Callback = callback,
				DirectionIsBackwards = false,
				EndDate = endDate,
				ParseFunction = (JToken token) =>
						token.ParseTrade(
								"amount",
								"price",
								"type",
								"timestampms",
								TimestampType.UnixMilliseconds,
								idKey: "tid"
						),
				StartDate = startDate,
				MarketSymbol = marketSymbol,
				TimestampFunction = (DateTime dt) =>
						(
								(long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(dt)
						).ToStringInvariant(),
				Url = "/trades/[marketSymbol]?limit_trades=100&timestamp={0}"
			};
			await state.ProcessHistoricalTrades();
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			JArray obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JArray>(
					"/balances",
					null,
					await GetNoncePayloadAsync()
			);
			var q =
					from JToken token in obj
					select new
					{
						Currency = token["currency"].ToStringInvariant(),
						Available = token["amount"].ConvertInvariant<decimal>()
					};
			foreach (var kv in q)
			{
				if (kv.Available > 0m)
				{
					lookup[kv.Currency] = kv.Available;
				}
			}
			return lookup;
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			JArray obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JArray>(
					"/balances",
					null,
					await GetNoncePayloadAsync()
			);
			var q =
					from JToken token in obj
					select new
					{
						Currency = token["currency"].ToStringInvariant(),
						Available = token["available"].ConvertInvariant<decimal>()
					};
			foreach (var kv in q)
			{
				if (kv.Available > 0m)
				{
					lookup[kv.Currency] = kv.Available;
				}
			}
			return lookup;
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			if (order.OrderType == OrderType.Market)
			{
				throw new NotSupportedException("Order type " + order.OrderType + " not supported");
			}

			object nonce = await GenerateNonceAsync();
			Dictionary<string, object> payload = new Dictionary<string, object>
						{
								{ "nonce", nonce },
								{
										"client_order_id",
										"ExchangeSharp_"
												+ CryptoUtility.UtcNow.ToString(
														"s",
														System.Globalization.CultureInfo.InvariantCulture
												)
								},
								{ "symbol", order.MarketSymbol },
								{ "amount", order.RoundAmount().ToStringInvariant() },
								{ "price", order.Price.ToStringInvariant() },
								{ "side", (order.IsBuy ? "buy" : "sell") },
								{ "type", "exchange limit" }
						};
			if (order.IsPostOnly == true)
				payload["options"] = "[maker-or-cancel]"; // This order will only add liquidity to the order book. If any part of the order could be filled immediately, the whole order will instead be canceled before any execution occurs. If that happens, the response back from the API will indicate that the order has already been canceled("is_cancelled": true in JSON). Note: some other exchanges call this option "post-only".
			order.ExtraParameters.CopyTo(payload);
			JToken obj = await MakeJsonRequestAsync<JToken>("/order/new", null, payload);
			return ParseOrder(obj);
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			if (string.IsNullOrWhiteSpace(orderId))
			{
				return null;
			}

			object nonce = await GenerateNonceAsync();
			var payload = new Dictionary<string, object>
						{
								{ "nonce", nonce },
								{ isClientOrderId ? "client_order_id" : "order_id", orderId }
						}; // client_order_id cannot be used in combination with order_id
			JToken result = await MakeJsonRequestAsync<JToken>(
					"/order/status",
					null,
					payload: payload
			);
			return ParseOrder(result);
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			object nonce = await GenerateNonceAsync();
			JToken result = await MakeJsonRequestAsync<JToken>(
					"/orders",
					null,
					new Dictionary<string, object> { { "nonce", nonce } }
			);
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

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			if (isClientOrderId)
				throw new NotSupportedException(
						"Cancelling by client order ID is not supported in ExchangeSharp. Please submit a PR if you are interested in this feature"
				);
			object nonce = await GenerateNonceAsync();
			await MakeJsonRequestAsync<JToken>(
					"/order/cancel",
					null,
					new Dictionary<string, object> { { "nonce", nonce }, { "order_id", orderId } }
			);
		}

		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(
				Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickerCallback,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			ConcurrentDictionary<string, decimal> volumeDict =
					new ConcurrentDictionary<string, decimal>();
			ConcurrentDictionary<string, ExchangeTicker> tickerDict =
					new ConcurrentDictionary<string, ExchangeTicker>();
			static ExchangeTicker GetTicker(
					ConcurrentDictionary<string, ExchangeTicker> tickerDict,
					ExchangeGeminiAPI api,
					string marketSymbol
			)
			{
				return tickerDict.GetOrAdd(
						marketSymbol,
						(_marketSymbol) =>
						{
							(string baseCurrency, string quoteCurrency) =
													api.ExchangeMarketSymbolToCurrenciesAsync(_marketSymbol).Sync();
							return new ExchangeTicker
							{
								Exchange = api.Name,
								MarketSymbol = _marketSymbol,
								Volume = new ExchangeVolume
								{
									BaseCurrency = baseCurrency,
									QuoteCurrency = quoteCurrency
								}
							};
						}
				);
			}

			static void PublishTicker(
					ExchangeTicker ticker,
					string marketSymbol,
					ConcurrentDictionary<string, decimal> _volumeDict,
					Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback
			)
			{
				// if we are fully populated...
				if (
						ticker.Bid > 0m
						&& ticker.Ask > 0m
						&& ticker.Bid <= ticker.Ask
						&& _volumeDict.TryGetValue(marketSymbol, out decimal tickerVolume)
				)
				{
					ticker.Volume.BaseCurrencyVolume = tickerVolume;
					ticker.Volume.QuoteCurrencyVolume = tickerVolume * ticker.Last;
					var kv = new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker);
					callback(new KeyValuePair<string, ExchangeTicker>[] { kv });
				}
			}

			return await ConnectPublicWebSocketAsync(
					null,
					messageCallback: (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token["result"].ToStringInvariant() == "error")
						{
							// {{  "result": "error",  "reason": "InvalidJson"}}
							Logger.Info(token["reason"].ToStringInvariant());
							return Task.CompletedTask;
						}
						string type = token["type"].ToStringInvariant();
						switch (type)
						{
							case "candles_1d_updates":

								{
									JToken changesToken = token["changes"];
									if (changesToken != null)
									{
										string marketSymbol = token["symbol"].ToStringInvariant();
										if (changesToken.FirstOrDefault() is JArray candleArray)
										{
											decimal volume = candleArray[5].ConvertInvariant<decimal>();
											volumeDict[marketSymbol] = volume;
											ExchangeTicker ticker = GetTicker(
																tickerDict,
																this,
																marketSymbol
														);
											PublishTicker(
																ticker,
																marketSymbol,
																volumeDict,
																tickerCallback
														);
										}
									}
								}
								break;

							case "l2_updates":

								{
									// fetch the last bid/ask/last prices
									if (token["trades"] is JArray tradesToken)
									{
										string marketSymbol = token["symbol"].ToStringInvariant();
										ExchangeTicker ticker = GetTicker(
															tickerDict,
															this,
															marketSymbol
													);
										JToken lastSell = tradesToken.FirstOrDefault(
															t =>
																	t["side"]
																			.ToStringInvariant()
																			.Equals("sell", StringComparison.OrdinalIgnoreCase)
													);
										if (lastSell != null)
										{
											decimal lastTradePrice = lastSell[
																"price"
														].ConvertInvariant<decimal>();
											ticker.Bid = ticker.Last = lastTradePrice;
										}
										JToken lastBuy = tradesToken.FirstOrDefault(
															t =>
																	t["side"]
																			.ToStringInvariant()
																			.Equals("buy", StringComparison.OrdinalIgnoreCase)
													);
										if (lastBuy != null)
										{
											decimal lastTradePrice = lastBuy[
																"price"
														].ConvertInvariant<decimal>();
											ticker.Ask = ticker.Last = lastTradePrice;
										}

										PublishTicker(ticker, marketSymbol, volumeDict, tickerCallback);
									}
								}
								break;

							case "trade":

								{
									//{ "type":"trade","symbol":"ETHUSD","event_id":35899433249,"timestamp":1619191314701,"price":"2261.65","quantity":"0.010343","side":"buy"}

									// fetch the active ticker metadata for this symbol
									string marketSymbol = token["symbol"].ToStringInvariant();
									ExchangeTicker ticker = GetTicker(tickerDict, this, marketSymbol);
									string side = token["side"].ToStringInvariant();
									decimal price = token["price"].ConvertInvariant<decimal>();
									if (side == "sell")
									{
										ticker.Bid = ticker.Last = price;
									}
									else
									{
										ticker.Ask = ticker.Last = price;
									}
									PublishTicker(ticker, marketSymbol, volumeDict, tickerCallback);
								}
								break;
						}
						return Task.CompletedTask;
					},
					connectCallback: async (_socket) =>
					{
						volumeDict.Clear();
						tickerDict.Clear();
						await _socket.SendMessageAsync(
											new
											{
												type = "subscribe",
												subscriptions = new[]
													{
																new { name = "candles_1d", symbols = marketSymbols },
																new { name = "l2", symbols = marketSymbols }
											}
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

			return await ConnectPublicWebSocketAsync(
					BaseUrlWebSocket,
					messageCallback: async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token["result"].ToStringInvariant() == "error")
						{
							// {{  "result": "error",  "reason": "InvalidJson"}}
							Logger.Info(token["reason"].ToStringInvariant());
						}
						else if (token["type"].ToStringInvariant() == "l2_updates")
						{
							string marketSymbol = token["symbol"].ToStringInvariant();
							var tradesToken = token["trades"];
							if (tradesToken != null)
								foreach (var tradeToken in tradesToken)
								{
									var trade = ParseWebSocketTrade(tradeToken);
									trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
									await callback(
														new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
												);
								}
						}
						else if (token["type"].ToStringInvariant() == "trade")
						{
							string marketSymbol = token["symbol"].ToStringInvariant();
							var trade = ParseWebSocketTrade(token);
							await callback(
												new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
										);
						}
					},
					connectCallback: async (_socket) =>
					{
						//{ "type": "subscribe","subscriptions":[{ "name":"l2","symbols":["BTCUSD","ETHUSD","ETHBTC"]}]}
						await _socket.SendMessageAsync(
											new
											{
												type = "subscribe",
												subscriptions = new[] { new { name = "l2", symbols = marketSymbols } }
											}
									);
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
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}

			return await ConnectPublicWebSocketAsync(
					string.Empty,
					(_socket, msg) =>
					{
						string message = msg.ToStringFromUTF8();
						var book = new ExchangeOrderBook();

						if (message.Contains("l2_updates"))
						{
							// parse delta update
							var delta = JsonConvert.DeserializeObject(message) as JObject;

							var symbol = delta["symbol"].ToString();
							book.MarketSymbol = symbol;

							// Gemini doesn't have a send timestamp in their response so use received timestamp.
							book.LastUpdatedUtc = DateTime.UtcNow;

							// Gemini doesn't have a sequence id in their response so use timestamp ticks.
							book.SequenceId = DateTime.Now.Ticks;

							foreach (JArray change in delta["changes"])
							{
								if (change.Count == 3)
								{
									bool sell = change[0].ToStringInvariant() == "sell";
									decimal price = change[1].ConvertInvariant<decimal>();
									decimal amount = change[2].ConvertInvariant<decimal>();

									SortedDictionary<decimal, ExchangeOrderPrice> dict = (
														sell ? book.Asks : book.Bids
												);

									dict[price] = new ExchangeOrderPrice
									{
										Amount = amount,
										Price = price
									};
								}
							}

							callback(book);
						}

						return Task.CompletedTask;
					},
					connectCallback: async (_socket) =>
					{
						await _socket.SendMessageAsync(
											new
											{
												type = "subscribe",
												subscriptions = new[] { new { name = "l2", symbols = marketSymbols } }
											}
									);
					}
			);
		}

		private static ExchangeTrade ParseWebSocketTrade(JToken token) =>
				token.ParseTrade(
						amountKey: "quantity",
						priceKey: "price",
						typeKey: "side",
						timestampKey: "timestamp",
						TimestampType.UnixMilliseconds,
						idKey: "event_id"
				);
	}

	public partial class ExchangeName
	{
		public const string Gemini = "Gemini";
	}
}
