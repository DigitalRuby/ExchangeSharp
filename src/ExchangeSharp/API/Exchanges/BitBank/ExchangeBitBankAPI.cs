using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static ExchangeSharp.CryptoUtility;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBitBankAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://public.bitbank.cc";
        public string BaseUrlPrivate { get; set; } = "https://api.bitbank.cc/v1";
        public string ErrorCodeDescriptionUrl { get; set; } = "https://docs.bitbank.cc/error_code/";


        // bitbank trade fees are fixed
        private const decimal MakerFee = -0.0005m;
        private const decimal TakerFee = 0.0015m;

        static ExchangeBitBankAPI()
        {
            ExchangeGlobalCurrencyReplacements[typeof(ExchangeBitBankAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("BCC", "BCH")
            };
        }

        public ExchangeBitBankAPI()
        {
            NonceStyle = NonceStyle.UnixMilliseconds;
            NonceOffset = TimeSpan.FromSeconds(0.1);
            WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
            MarketSymbolSeparator = "_";
            MarketSymbolIsUppercase = false;
        }

        # region Public APIs

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken token = await MakeJsonRequestAsync<JToken>($"/{marketSymbol}/ticker");
            return await ParseTickerAsync(marketSymbol, token);
        }

        // Bitbank supports endpoint for getting all rates in one request, Using this endpoint is faster then ExchangeAPI's default implementation
        // (which interate `OnGetTickerAsync` for each marketSymbols)
        // Note: This doesn't give you a volume. if you want it, please specify marketSymbol.
        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            JToken token = await MakeJsonRequestAsync<JToken>($"/prices");
            var symbols = await OnGetMarketSymbolsAsync();
            var result = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (var symbol in symbols)
            {
                var data = token[GlobalMarketSymbolToExchangeMarketSymbolAsync(symbol)];
                var ticker = new ExchangeTicker()
                {
                    Ask = data["sell"].ConvertInvariant<decimal>(),
                    Bid = data["buy"].ConvertInvariant<decimal>(),
                    Last = data["last"].ConvertInvariant<decimal>(),
                    MarketSymbol = symbol
                };
                result.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
            }
            return result;
        }

        #endregion

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>($"/{marketSymbol}/transactions");
            ExchangeOrderBook result = new ExchangeOrderBook();
            // we can not use `APIExtensions.ParseOrderBookFromJToken ...` here, because bid/ask is denoted by "side" property.
            foreach (JToken tx in token["transactions"])
            {
                var isBuy = (string)tx["side"] == "buy";
                decimal price = tx["price"].ConvertInvariant<decimal>();
                decimal amount = tx["amount"].ConvertInvariant<decimal>();
                if (isBuy)
                {
                    result.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                }
                else
                {
                    result.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                }
                result.MarketSymbol = NormalizeMarketSymbol(marketSymbol);
            }
            return result;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            var period = FormatPeriod(periodSeconds);
            var url = $"/{marketSymbol}/candlestick/{period}/{startDate?.ToString("yyyyMMdd")}";
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            List <MarketCandle> result = new List<MarketCandle>();
            // since it is impossible to convert by `CryptoUtility.ToDateTimeInvariant()`
            foreach (var c in token["candlestick"])
            {
                foreach (var data in c["ohlcv"])
                {
                    var open = data[0].ConvertInvariant<decimal>();
                    var timestamp = DateTime.SpecifyKind(data[5].ConvertInvariant<double>().UnixTimeStampToDateTimeMilliseconds(), DateTimeKind.Utc);
                    var candle = new MarketCandle()
                    {
                        ExchangeName = "BitBank",
                        Name = url,
                        OpenPrice = open,
                        HighPrice = data[1].ConvertInvariant<decimal>(),
                        LowPrice = data[2].ConvertInvariant<decimal>(),
                        ClosePrice = data[3].ConvertInvariant<decimal>(),
                        BaseCurrencyVolume = data[4].ConvertInvariant<double>(),
                        Timestamp = timestamp,
                    };
                    result.Add(candle);
                }
            }
            return result;
        }

        # region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync() => await OnGetAmountsAsyncCore("onhand_amount");

        protected override Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            throw new NotImplementedException();
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Stop)
                throw new InvalidOperationException("Bitbank does not support stop order");
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload.Add("pair", NormalizeMarketSymbol(order.MarketSymbol));
            payload.Add("amount", order.Amount.ToStringInvariant());
            payload.Add("side", order.IsBuy ? "buy" : "sell");
            payload.Add("type", order.OrderType.ToStringLowerInvariant());
            payload.Add("price", order.Price);
            JToken token = await MakeJsonRequestAsync<JToken>("/user/spot/order", baseUrl: BaseUrlPrivate, payload: payload, requestMethod: "POST");
            return ParseOrder(token);
        }
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (marketSymbol == null)
                throw new APIException("Bitbank requries market symbol when cancelling orders");
            payload.Add("pair", NormalizeMarketSymbol(marketSymbol));
            payload.Add("order_id", orderId);
            await MakeJsonRequestAsync<JToken>("/user/spot/cancel_order", baseUrl: BaseUrlPrivate, payload: payload, requestMethod: "POST");
        }
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            var payload = await GetNoncePayloadAsync();
            payload.Add("order_id", orderId);
            if (marketSymbol == null)
                throw new InvalidOperationException($"BitBank API requires marketSymbol for {nameof(GetOrderDetailsAsync)}");
            payload.Add("pair", marketSymbol);
            JToken token = await MakeJsonRequestAsync<JToken>("/user/spot/order", baseUrl: BaseUrlPrivate, payload: payload);
            return ParseOrder(token);
        }
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            var payload = await GetNoncePayloadAsync();
            if (marketSymbol != null)
                payload.Add("pair", NormalizeMarketSymbol(marketSymbol));
            JToken token = await MakeJsonRequestAsync<JToken>("/user/spot/active_orders", baseUrl: BaseUrlPrivate, payload: payload);
            return token["orders"].Select(o => ParseOrder(o));
        }
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            var payload = await GetNoncePayloadAsync();
            if (marketSymbol == null)
                throw new APIException("BitBank requires marketSymbol when getting completed orders");
            payload.Add("pair", NormalizeMarketSymbol(marketSymbol));
            if (afterDate != null)
                payload.Add("since", afterDate.ConvertInvariant<double>());
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/spot/trade_history", baseUrl: BaseUrlPrivate, payload: payload);
            return token["trades"].Select(t => TradeHistoryToExchangeOrderResult(t));
        }

        /// <summary>
        /// Bitbank does not support withdrawing to arbitrary address (for security reason).
        /// We must first register address from its web form.
        /// So we will call two methods here.
        /// 1. Get address from already registered account. (fail if does not exist)
        /// 2. Withdraw to that address.
        /// </summary>
        /// <param name="withdrawalRequest"></param>
        /// <returns></returns>
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            var asset = withdrawalRequest.Currency.ToLowerInvariant();
            var payload1 = await GetNoncePayloadAsync();
            payload1.Add("asset", asset);
            JToken token1 = await MakeJsonRequestAsync<JToken>($"/user/withdrawal_account", baseUrl: BaseUrlPrivate, payload: payload1);
            if (!token1["accounts"].ToArray().Any(a => a["address"].ToStringInvariant() == withdrawalRequest.Address))
                throw new APIException($"Could not withdraw to address {withdrawalRequest.Address}! You must register the address from web form first.");

            var uuid = token1["uuid"].ToStringInvariant();

            var payload2 = await GetNoncePayloadAsync();
            payload2.Add("asset", asset);
            payload2.Add("amount", withdrawalRequest.Amount);
            payload2.Add("uuid", uuid);
            JToken token2 = await MakeJsonRequestAsync<JToken>($"/user/request_withdrawal", baseUrl: BaseUrlPrivate, payload: payload2, requestMethod: "POST");
            var resp = new ExchangeWithdrawalResponse
            {
                Id = token2["txid"].ToStringInvariant()
            };
            var status = token2["status"].ToStringInvariant();
            resp.Success = status != "REJECTED" && status != "CANCELED";
            resp.Message = "{" + $"label:{token2["label"]}, fee:{token2["fee"]}" + "}";
            return resp;
        }

        # endregion

        /// <summary>
        /// BitBank does not support API for this one. So hard-code it.
        /// </summary>
        /// <returns></returns>
        protected override Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return Task.FromResult(new List<string> {
                "btc_jpy",
                "xrp_jpy",
                "ltc_btc",
                "eth_btc",
                "mona_jpy",
                "mona_btc",
                "bcc_jpy",
                "bcc_btc"
                }.AsEnumerable());
        }

        // protected override Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync() => throw new NotImplementedException();

        // protected override Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync() => throw new NotImplementedException();
        // protected override Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false) => throw new NotImplementedException();
        // protected override Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency) => throw new NotImplementedException();
        // protected override Task<Dictionary<string, decimal>> OnGetFeesAsync() => throw new NotImplementedException();
        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
            => await OnGetAmountsAsyncCore("free_amount");

        /// <summary>
        /// Bitbank does not support placing several orders at once, so we will just run `PlaceOrderAsync` for each orders.
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order)
        {
            var resp = new List<ExchangeOrderResult>();
            foreach (var o in order)
                resp.Add(await this.PlaceOrderAsync(o));
            return resp.ToArray();
        }
        // protected override Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(string currency) => throw new NotImplementedException();
        // protected override Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances) => throw new NotImplementedException();
        // protected override Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string marketSymbol) => throw new NotImplementedException();
        // protected override Task<ExchangeCloseMarginPositionResult> OnCloseMarginPositionAsync(string marketSymbol) => throw new NotImplementedException();
        /*
        */

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload) && method == "GET" && payload.Count != 0)
            {
                var realPayload = new Dictionary<string, object>();
                payload.CopyTo(realPayload);
                realPayload.Remove("nonce");
                CryptoUtility.AppendPayloadToQuery(url, realPayload);
            }
            return base.ProcessRequestUrl(url, payload, method);
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                var stringToCommit = String.Empty;
                if (request.Method == "POST")
                {
                    var msg = CryptoUtility.GetJsonForPayload(payload);
                    stringToCommit = $"{nonce}{msg}";
                    await request.WritePayloadJsonToRequestAsync(payload);
                }
                else if (request.Method == "GET")
                {
                    stringToCommit = $"{nonce}{request.RequestUri.PathAndQuery}";
                }
                else
                {
                    throw new APIException($"BitBank does not support {request.Method} as its HTTP method!");
                }
                string signature = CryptoUtility.SHA256Sign(stringToCommit, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));

                request.AddHeader("ACCESS-NONCE", nonce.ToStringInvariant());
                request.AddHeader("ACCESS-KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("ACCESS-SIGNATURE", signature);
            }
            return;
        }

        private async Task<ExchangeTicker> ParseTickerAsync(string symbol, JToken token)
        {
            return await this.ParseTickerAsync(token, symbol, "sell", "buy", "last", "vol", quoteVolumeKey: null, "timestamp", TimestampType.UnixMilliseconds);
        }

        private string FormatPeriod(int ps)
        {
            if (ps < 0)
                throw new APIException("Can not specify negative time for period");
            if (ps < 60)
                return "1min";
            if (ps < 300)
                return "5min";
            if (ps < 900)
                return "15min";
            if (ps < 1800)
                return "30min";
            else
                return "1hour";
            /* These are not working
            if (ps < 3600 * 4)
                return "4hour";
            if (ps < 3600 * 8)
                return "8hour";
            if (ps < 3600 * 12)
                return "12hour";
            else
                return "1day";
            */
        }
        
        private ExchangeOrderResult ParseOrder(JToken token)
        {
            var res = ParseOrderCore(token);
            res.Amount = token["executed_amount"].ConvertInvariant<decimal>();
            res.AveragePrice = token["averate_price"].ConvertInvariant<decimal>();
            res.AmountFilled = token["executed_amount"].ConvertInvariant<decimal>();
            res.OrderDate = token["ordered_at"].ConvertInvariant<double>().UnixTimeStampToDateTimeMilliseconds();
            switch (token["status"].ToStringInvariant())
            {
                case "UNFILLED":
                    res.Result = ExchangeAPIOrderResult.Pending;
                    break;
                case "PARTIALLY_FILLED":
                    res.Result = ExchangeAPIOrderResult.FilledPartially;
                    break;
                case "FULLY_FILLED":
                    res.Result = ExchangeAPIOrderResult.Filled;
                    break;
                case "CANCELED_UNFILLED":
                    res.Result = ExchangeAPIOrderResult.Canceled;
                    break;
                case "CANCELED_PARTIALLY_FILLED":
                    res.Result = ExchangeAPIOrderResult.FilledPartiallyAndCancelled;
                    break;
                default:
                    res.Result = ExchangeAPIOrderResult.Unknown;
                    break;
            }
            return res;
        }

        private ExchangeOrderResult TradeHistoryToExchangeOrderResult(JToken token)
        {
            var res = ParseOrderCore(token);
            res.TradeId = token["trade_id"].ToStringInvariant();
            res.Amount = token["amount"].ConvertInvariant<decimal>();
            res.AmountFilled = res.Amount;
            res.Fees = token["fee_amount_base"].ConvertInvariant<decimal>();
            res.Result = ExchangeAPIOrderResult.Filled;
            res.Message = token["maker_taker"].ToStringInvariant();
            return res;
        }

        // Parse common part of two kinds of response
        // 1. CompletedOrder details
        // 2. GetOrder, PostOrder
        private ExchangeOrderResult ParseOrderCore(JToken token)
        {
            var res = new ExchangeOrderResult
            {
                OrderId = token["order_id"].ToStringInvariant(),
                MarketSymbol = token["pair"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant() == "buy"
            };
            res.Fees = token["type"].ToStringInvariant() == "limit" ? MakerFee * res.Amount : TakerFee * res.Amount;
            res.Price = token["price"].ConvertInvariant<decimal>();
            res.FillDate = token["executed_at"] == null ? default : token["executed_at"].ConvertInvariant<double>().UnixTimeStampToDateTimeMilliseconds();
            res.FeesCurrency = res.MarketSymbol.Substring(0, 3);
            return res;
        }

        private async Task<Dictionary<string, decimal>> OnGetAmountsAsyncCore(string type)
        {
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/assets", baseUrl: BaseUrlPrivate, payload: await GetNoncePayloadAsync(), requestMethod: "GET");
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken assets in token["assets"])
            {
                decimal amount = assets[type].ConvertInvariant<decimal>();
                if (amount > 0m)
                    balances[assets["assets"].ToStringInvariant()] = amount;
            }
            return balances;
        }
    }
    public partial class ExchangeName { public const string BitBank = "BitBank"; }
}
