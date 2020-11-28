using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExchangeSharp.NDAX;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeNDAXAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.ndax.io:8443/AP";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.ndax.io/WSGateway";
        
        private AuthenticateResult authenticationDetails = null;
        public override string Name => ExchangeName.NDAX;

        private static Dictionary<string, long> _marketSymbolToInstrumentIdMapping;
        private static Dictionary<string, long> _symbolToProductId;

        public ExchangeNDAXAPI()
        {
            RequestContentType = "application/json";
            MarketSymbolSeparator = "_";
            RequestMethod = "POST";
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var result =
                await MakeJsonRequestAsync<Dictionary<string, NDAXTicker>>("returnticker", "https://ndax.io/api", null, "GET");
            _marketSymbolToInstrumentIdMapping = result.ToDictionary(pair => pair.Key.Replace("_", ""), pair => pair.Value.Id); // remove the _
            return result.Select(pair =>
                new KeyValuePair<string, ExchangeTicker>(pair.Key, pair.Value.ToExchangeTicker(pair.Key)));
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            return (await GetTickersAsync()).Single(pair => pair.Key.Equals(symbol, StringComparison.InvariantCultureIgnoreCase)).Value;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await OnGetMarketSymbolsMetadataAsync()).Select(market => market.MarketSymbol);
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<NDAXProduct>>("GetProducts", null,
                new Dictionary<string, object>()
                    { {"OMSId", 1}}, "POST");
            _symbolToProductId = result.ToDictionary(product => product.Product, product => product.ProductId);
            return result.ToDictionary(product => product.Product, product => product.ToExchangeCurrency());
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<Instrument>>("GetInstruments", null,
                new Dictionary<string, object>()
                    { {"OMSId", 1}}, "POST");
			_marketSymbolToInstrumentIdMapping = result.ToDictionary(keySelector: instrument => instrument.Symbol.Replace("_", ""),
				elementSelector: instrument => instrument.InstrumentId);
			return result.Select(instrument => instrument.ToExchangeMarket());
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null,
            DateTime? afterDate = null)
        {
            var payload = new Dictionary<string, object>()
            {
                {"nonce", await GenerateNonceAsync()}
            };

            if (afterDate.HasValue)
            {
                payload.Add("StartTimeStamp", new DateTimeOffset( afterDate.Value).ToUniversalTime().ToUnixTimeMilliseconds());
            }
            if (symbol != null)
            {
                payload.Add("InstrumentId", await GetInstrumentIdFromMarketSymbol(symbol));
            }
            var result = await MakeJsonRequestAsync<IEnumerable<Order>>("GetOrderHistory", null,
                payload, "POST");

            return result.Select(order => order.ToExchangeOrderResult(_marketSymbolToInstrumentIdMapping));
        }
        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null,
            DateTime? endDate = null, int? limit = null)
        {

            var payload = new Dictionary<string, object>()
            {
                {"nonce", await GenerateNonceAsync()}
            };

            if (marketSymbol != null)
            {
                payload.Add("InstrumentId", await GetInstrumentIdFromMarketSymbol(marketSymbol));
            }

            if (startDate.HasValue)
            {
                payload.Add("StartTimeStamp", new DateTimeOffset( startDate.Value).ToUniversalTime().ToUnixTimeMilliseconds());
            }
            if (endDate.HasValue)
            {
                payload.Add("EndTimeStamp", new DateTimeOffset( endDate.Value).ToUniversalTime().ToUnixTimeMilliseconds());
            }
            var result = await MakeJsonRequestAsync<IEnumerable<TradeHistory>>("GetTradesHistory", null,
                payload, "POST");

            callback.Invoke( result.Select(history => history.ToExchangeTrade()));
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol,
            bool forceRegenerate = false)
        {
            var result = await MakeJsonRequestAsync<NDAXDepositInfo>("GetDepositInfo", null,
                new Dictionary<string, object>()
                {
                    {"ProductId", await GetProductIdFromCryptoCode(symbol)},
                    {"GenerateNewKey", forceRegenerate},
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");

            return result.ToExchangeDepositDetails(symbol);
        }


        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<AccountBalance>>("GetAccountPositions", null,
                new Dictionary<string, object>()
                {
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");
            return result.ToDictionary(balance => balance.ProductSymbol, balance => balance.Amount);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<AccountBalance>>("GetAccountPositions", null,
                new Dictionary<string, object>()
                {
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");
            return result.ToDictionary(balance => balance.ProductSymbol, balance => balance.Amount- balance.Hold);
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var orderType = 0;
            var side = order.IsBuy? 0: 1;
            switch (order.OrderType)
            {
                case OrderType.Market:
                    orderType = 1;
                    break;
                case OrderType.Limit:
                    orderType = 2;
                    break;

                case OrderType.Stop:
                    orderType = 3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var result = await MakeJsonRequestAsync<SendOrderResponse>("SendOrder", null,
                new Dictionary<string, object>()
                {
                    {"InstrumentId", await GetInstrumentIdFromMarketSymbol(order.MarketSymbol)},
                    {"Quantity", order.Amount},
                    {"LimitPrice", order.Price},
                    {"OrderType", orderType},
                    {"Side", side},
                    {"StopPrice", order.StopPrice},
                    {"TimeInForce", 0},
                    {"nonce", await GenerateNonceAsync()}
                }.Concat(order.ExtraParameters).ToDictionary(pair => pair.Key, pair =>pair.Value ), "POST");

            if (result.Status.Equals("accepted", StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetOrderDetailsAsync(result.OrderId.ToString());
            }
            throw new APIException($"{result.ErrorMsg}");
        }

        protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order)
        {
            var resp = new List<ExchangeOrderResult>();
            foreach (var o in order)
                resp.Add(await PlaceOrderAsync(o));
            return resp.ToArray();
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            await EnsureInstrumentIdsAvailable();
            var result = await MakeJsonRequestAsync<Order>("GetOrderStatus", null,
                new Dictionary<string, object>()
                {
                    {"OrderId", int.Parse(orderId)},
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");
            return result.ToExchangeOrderResult(_marketSymbolToInstrumentIdMapping);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            await EnsureInstrumentIdsAvailable();
            var result = await MakeJsonRequestAsync<IEnumerable<Order>>("GetOpenOrders", null,
                new Dictionary<string, object>()
                {
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");
            return result.Select(order => order.ToExchangeOrderResult(_marketSymbolToInstrumentIdMapping)).Where(
                orderResult =>
                    symbol == null ||
                    orderResult.MarketSymbol.Equals(symbol, StringComparison.InvariantCultureIgnoreCase));
        }


        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            var result = await MakeJsonRequestAsync<GenericResponse>("CancelOrder", null,
                new Dictionary<string, object>()
                {
                    {"OrderId", orderId},
                    {"nonce", await GenerateNonceAsync()}
                }, "POST");
            if (!result.Result)
            {
                throw new APIException($"{result.ErrorCode}:{result.ErrorMsg}");
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null,
            int? limit = null)
        {
            var payload = new Dictionary<string, object>()
            {
                {"InstrumentId", await GetInstrumentIdFromMarketSymbol(marketSymbol)}
            };
            if (startDate.HasValue)
            {
                payload.Add("FromDate", new DateTimeOffset( startDate.Value).ToUniversalTime().ToUnixTimeMilliseconds());
            }

            var result = await MakeJsonRequestAsync<IEnumerable<IEnumerable<JToken>>>("GetTickerHistory", null, payload
               , "POST");

            return result.Select(enumerable => new MarketCandle()
            {
                Name = marketSymbol,
                ExchangeName = ExchangeName.NDAX,
                Timestamp = enumerable.ElementAt(0).Value<long>().UnixTimeStampToDateTimeMilliseconds(),
                HighPrice = enumerable.ElementAt(1).Value<decimal>(),
                LowPrice = enumerable.ElementAt(2).Value<decimal>(),
                OpenPrice = enumerable.ElementAt(3).Value<decimal>(),
                ClosePrice = enumerable.ElementAt(4).Value<decimal>(),
                BaseCurrencyVolume = enumerable.ElementAt(5).Value<double>(),

            }).Where(candle => !endDate.HasValue || candle.Timestamp <= endDate);
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (PrivateApiKey != null && PublicApiKey != null)
            {
                request.AddHeader("Authorization",
                    CryptoUtility.BasicAuthenticationString(PublicApiKey.ToUnsecureString(),
                        PrivateApiKey.ToUnsecureString()));
            }

            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("apToken", authenticationDetails.Token);
                payload.Add("OMSId", authenticationDetails.OMSId);
                payload.Add("AccountId", authenticationDetails.AccountId);

            }

            if (request.Method == "POST")
            {
                await request.WritePayloadJsonToRequestAsync(payload);
            }
        }

        protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
        {
            if (base.CanMakeAuthenticatedRequest(payload) && !payload.ContainsKey("skipauthrequest"))
            {
                if (!(authenticationDetails?.Authenticated ?? false))
                {
                    Authenticate().GetAwaiter().GetResult();
                }

                return authenticationDetails?.Authenticated ?? false;
            }

            return false;
        }

        private async Task Authenticate()
        {
            authenticationDetails = await MakeJsonRequestAsync<AuthenticateResult>("Authenticate", null,
                new Dictionary<string, object>()
                {
                    {"skipauthrequest", true}
                }, "POST");
        }

        private async Task EnsureProductIdsAvailable()
        {
            if (_symbolToProductId == null)
            {
                await GetCurrenciesAsync();
            }
        }

        private async Task EnsureInstrumentIdsAvailable()
        {
            if (_marketSymbolToInstrumentIdMapping == null)
            {
				await OnGetMarketSymbolsMetadataAsync();
            }
        }

        private async Task<long?> GetProductIdFromCryptoCode(string cryptoCode)
        {
            cryptoCode = cryptoCode.ToUpperInvariant();
            await EnsureProductIdsAvailable();
            if (_symbolToProductId.TryGetValue(cryptoCode, out var value))
            {
                return value;
            }

            return null;
        }

        private async Task<long?> GetInstrumentIdFromMarketSymbol(string marketSymbol)
        {
            marketSymbol = marketSymbol.ToUpperInvariant();
            await EnsureInstrumentIdsAvailable();
			if (_marketSymbolToInstrumentIdMapping.TryGetValue(marketSymbol, out var value))
			{
				return value;
			}
			else if (_marketSymbolToInstrumentIdMapping.TryGetValue(marketSymbol.Replace("_", ""), out var value2))
			{ // try again w/o the _
				return value2;
			}

			return null;
        }

        private async Task<long?[]> GetInstrumentIdFromMarketSymbol(string[] marketSymbols)
        {
            return await Task.WhenAll(marketSymbols.Select(GetInstrumentIdFromMarketSymbol));
        }

        private async Task<string> GetMarketSymbolFromInstrumentId(long instrumentId)
        {
            await EnsureInstrumentIdsAvailable();
            var match = _marketSymbolToInstrumentIdMapping.Where(pair => pair.Value == instrumentId);
            return match.Any() ? match.First().Key : null;
        }

        protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers, params string[] marketSymbols)
        {
			await EnsureInstrumentIdsAvailable();
			var instrumentIds = marketSymbols == null || marketSymbols.Length == 0 ?
				(await GetMarketSymbolsMetadataAsync()).Select(s => (long?)long.Parse(s.AltMarketSymbol)).ToArray() :
				await GetInstrumentIdFromMarketSymbol(marketSymbols);

			return await ConnectWebSocketAsync("", async (socket, bytes) =>
                {
                    var messageFrame =
                        JsonConvert.DeserializeObject<MessageFrame>(bytes.ToStringFromUTF8().TrimEnd('\0'));

                    if (messageFrame.FunctionName.Equals("SubscribeLevel1", StringComparison.InvariantCultureIgnoreCase)
                        || messageFrame.FunctionName.Equals("Level1UpdateEvent",
                            StringComparison.InvariantCultureIgnoreCase))
                    {
						var token = JToken.Parse(messageFrame.Payload);
						if (token["errormsg"] == null)
						{
							var rawPayload = messageFrame.PayloadAs<Level1Data>();
							var symbol = await GetMarketSymbolFromInstrumentId(rawPayload.InstrumentId);
							tickers.Invoke(new[]
							{
								new KeyValuePair<string, ExchangeTicker>(symbol, rawPayload.ToExchangeTicker(symbol)),
							});
						}
						else // "{\"result\":false,\"errormsg\":\"Resource Not Found\",\"errorcode\":104,\"detail\":\"Instrument not Found\"}"
							Logger.Info(messageFrame.Payload);
					}
				},
                async socket =>
                {
                    foreach (var instrumentId in instrumentIds)
                    {
                        await socket.SendMessageAsync(new MessageFrame
                        {
                            FunctionName = "SubscribeLevel1",
                            MessageType = MessageType.Request,
                            SequenceNumber = GetNextSequenceNumber(),
                            Payload = JsonConvert.SerializeObject(new
                            {
                                OMSId = 1,
                                InstrumentId = instrumentId,

                            })
                        });
                    }
                });
        }

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			long?[] instrumentIds;
			if (marketSymbols == null || marketSymbols.Length == 0)
				instrumentIds = (await GetMarketSymbolsMetadataAsync()).Select(s => (long?)long.Parse(s.AltMarketSymbol)).ToArray();
			else
			{
				await EnsureInstrumentIdsAvailable();
				instrumentIds = await GetInstrumentIdFromMarketSymbol(marketSymbols);
			}
			return await ConnectWebSocketAsync("", async (socket, bytes) =>
				{
					var messageFrame =
						JsonConvert.DeserializeObject<MessageFrame>(bytes.ToStringFromUTF8().TrimEnd('\0'));

					if (messageFrame.FunctionName.Equals("SubscribeTrades", StringComparison.InvariantCultureIgnoreCase)
						|| messageFrame.FunctionName.Equals("OrderTradeEvent", StringComparison.InvariantCultureIgnoreCase)
						|| messageFrame.FunctionName.Equals("TradeDataUpdateEvent", StringComparison.InvariantCultureIgnoreCase))
					{
						if (messageFrame.Payload != "[]")
						{
							var token = JToken.Parse(messageFrame.Payload);
							if (token.Type == JTokenType.Array)
							{ // "[[34838,2,0.4656,10879.5,311801351,311801370,1570134695227,1,0,0,0],[34839,2,0.4674,10881.7,311801352,311801370,1570134695227,1,0,0,0]]"
								var jArray = token as JArray;
								for (int i = 0; i < jArray.Count; i++)
								{
									var tradesToken = jArray[i];
									var symbol = await GetMarketSymbolFromInstrumentId(tradesToken[1].ConvertInvariant<long>());
									var trade = tradesToken.ParseTradeNDAX(amountKey: 2, priceKey: 3,
											typeKey: 8, timestampKey: 6,
											TimestampType.UnixMilliseconds, idKey: 0,
											typeKeyIsBuyValue: "0");
									if (messageFrame.FunctionName.Equals("SubscribeTrades", StringComparison.InvariantCultureIgnoreCase))
									{
										trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
										if (i == jArray.Count - 1)
										{
											trade.Flags |= ExchangeTradeFlags.IsLastFromSnapshot;
										}
									}
									await callback(
										new KeyValuePair<string, ExchangeTrade>(symbol, trade));
								}
							}
							else // "{\"result\":false,\"errormsg\":\"Invalid Request\",\"errorcode\":100,\"detail\":null}"
								Logger.Info(messageFrame.Payload);
						}
					}
				},
				async socket =>
				{
					foreach (var instrumentId in instrumentIds)
					{
						await socket.SendMessageAsync(new MessageFrame
						{
							FunctionName = "SubscribeTrades",
							MessageType = MessageType.Request,
							SequenceNumber = GetNextSequenceNumber(),
							Payload = JsonConvert.SerializeObject(new
							{
								OMSId = 1,
								InstrumentId = instrumentId,
								IncludeLastCount = 100,
							})
						});
					}
				});
		}

		private long GetNextSequenceNumber()
        {
            // Best practice is to carry an even sequence number.
            Interlocked.Add(ref _sequenceNumber, 2);

            return _sequenceNumber;
        }

        private long _sequenceNumber;

    }


    public partial class ExchangeName
    {
        public const string NDAX = "NDAX";
    }
}
