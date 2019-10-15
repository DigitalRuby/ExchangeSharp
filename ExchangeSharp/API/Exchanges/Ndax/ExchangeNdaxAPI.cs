using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.Ndax.Models;

namespace ExchangeSharp
{
    public sealed partial class ExchangeNdaxAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.ndax.io:8443/AP";
//        public override string BaseUrlWebSocket { get; set; } = "wss://stream.binance.com:9443";

        private AuthenticateResult authenticationDetails = null;
        public override string Name => ExchangeName.Ndax;

        private static Dictionary<string, long> _marketSymbolToInstrumentIdMapping;
        private static Dictionary<string, long> _symbolToProductId;

        public ExchangeNdaxAPI()
        {
            MarketSymbolSeparator = "_";
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var result =
                await MakeJsonRequestAsync<Dictionary<string, NdaxTicker>>("returnticker", "https://ndax.io/api");
            _marketSymbolToInstrumentIdMapping = result.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
            return result.Select(pair =>
                new KeyValuePair<string, ExchangeTicker>(pair.Key, pair.Value.ToExchangeTicker(pair.Key)));
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var result = await MakeJsonRequestAsync<Dictionary<string, NdaxTicker>>("returnticker",
                "https://ndax.io/api", new Dictionary<string, object>()
                {
                    {"InstrumentId", await GetInstrumentIdFromMarketSymbol(symbol)}
                });
            return result[symbol].ToExchangeTicker(symbol);
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await OnGetMarketSymbolsMetadataAsync()).Select(market => market.MarketSymbol);
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<NDaxProduct>>("GetProducts", null,
                new Dictionary<string, object>()
                    { }, "POST");
            _symbolToProductId = result.ToDictionary(product => product.Product, product => product.ProductId);
            return result.ToDictionary(product => product.Product, product => product.ToExchangeCurrency());
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var result = await MakeJsonRequestAsync<IEnumerable<Instrument>>("GetInstruments", null,
                new Dictionary<string, object>()
                    { }, "POST");

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
            var result = await MakeJsonRequestAsync<IEnumerable<Order>>("GetOrdersHistory", null,
                payload, "POST");

            return result.Select(order => order.ToExchangeOrderResult(_marketSymbolToInstrumentIdMapping));
        }
        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null,
            DateTime? endDate = null)
        {

            var payload = new Dictionary<string, object>()
            {
                {"ProductId", await GetProductIdFromCryptoCode(marketSymbol)},
                {"GenerateNewKey", true},
                {"nonce", await GenerateNonceAsync()}
            };

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
            var result = await MakeJsonRequestAsync<DepositInfo>("GetDepositInfo", null,
                new Dictionary<string, object>()
                {
                    {"ProductId", await GetProductIdFromCryptoCode(symbol)},
                    {"GenerateNewKey", true},
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
                case OrderType.Limit:
                    orderType = 2;
                    break;
                case OrderType.Market:
                    orderType = 1;
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
            return result.Select(order => order.ToExchangeOrderResult(_marketSymbolToInstrumentIdMapping));
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

//        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
//        {
//            
//            await EnsureProductIdsAvailable();
//            var template = await MakeJsonRequestAsync<WithdrawTemplates>("GetWithdrawTemplateTypes", null,
//                new Dictionary<string, object>()
//                {
//                    {"nonce", await GenerateNonceAsync()},
//                    {"ProductId", await GetProductIdFromCryptoCode(withdrawalRequest.Currency)},
//                    
//                }, "POST");
//            if (!template.Result)
//            {
//                throw  new APIException($"{template.ErrorCode}:{template.ErrorMsg}");
//            }
//            
//            if (!template.TemplateTypes.Any())
//            {
//                throw  new APIException($"No withdraw template available for {withdrawalRequest.Currency}");
//            }
//            var result = await MakeJsonRequestAsync<GenericResponse>("CreateWithdrawTicket", null,
//                new Dictionary<string, object>()
//                {
//                    {"nonce", await GenerateNonceAsync()},
//                    {"ProductId", await GetProductIdFromCryptoCode(withdrawalRequest.Currency)},
//                    {"Amount", withdrawalRequest.Amount},
//                    {"TemplateForm", new
//                    {
//                        TemplateType = template.TemplateTypes.First(),
//                        Comment = withdrawalRequest.Description,
//                        ExternalAddress = withdrawalRequest.Address
//                        
//                    }}
//                }, "POST");
//
//            if (!result.Result)
//            {
//                throw  new APIException($"{result.ErrorCode}:{result.ErrorMsg}");
//            }
//        }

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
                if (request.Method == "POST")
                {
                    await request.WritePayloadJsonToRequestAsync(payload);
                }
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
                await GetTickersAsync();
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

            return null;
        }

//        internal async Task<T> QueryAsync<T>(string functionName, object payload = null)
//        {
//            if (string.IsNullOrWhiteSpace(functionName))
//                throw new ArgumentNullException(nameof(functionName));
//
//            // Wrap all calls in a frame object.
//            var frame = new MessageFrame
//            {
//                FunctionName = functionName,
//                MessageType = MessageType.Request,
//                SequenceNumber = GetNextSequenceNumber(),
//                Payload = JsonConvert.SerializeObject(payload)
//            };
//            
//            var tcs = new TaskCompletionSource<T>();
//            var handlerFinished = tcs.Task;
//            using (ConnectWebSocketAsync("", (socket, bytes) =>
//            {
//                var messageFrame = JsonConvert.DeserializeObject<MessageFrame>(bytes.ToStringFromUTF8().TrimEnd('\0'));
//                tcs.SetResult(messageFrame.PayloadAs<T>());
//                return Task.CompletedTask;
//            }, async socket =>
//            {
//               await  socket.SendMessageAsync(frame);
//            }))
//            {
//                return await handlerFinished;
//            }
//        }

//        private long GetNextSequenceNumber()
//        {
//            // Best practice is to carry an even sequence number.
//            Interlocked.Add(ref _sequenceNumber, 2);
//
//            return _sequenceNumber;
//        }
//        
//        private long _sequenceNumber;
//        
    }


    public partial class ExchangeName
    {
        public const string Ndax = "Ndax";
    }
}