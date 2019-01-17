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
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Lbank API functionality.
    /// </summary>
    /// <remarks>
    /// Lbank API: https://github.com/LBank-exchange/lbank-official-api-docs
    /// Lbank site: https://www.lbank.info
    /// WebSockets address: ws://api.lbank.info/ws/v2/ 
    /// </remarks>
    public class ExchangeLBankAPI : ExchangeAPI
    {
        private const int ORDER_BOOK_MAX_SIZE = 60;
        private const int RECENT_TRADS_MAX_SIZE = 600;
        private const int WITHDRAW_PAGE_MAX_SIZE = 100;

        /// <summary>
        /// Base URL for the API.
        /// </summary>
        public override string BaseUrl { get; set; } = "https://api.lbank.info/v1";

        /// <summary>
        /// Gets the name of the API.
        /// </summary>
        public override string Name => ExchangeName.LBank;

        /// <summary>
        /// Constructor
        /// </summary>
        public ExchangeLBankAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            MarketSymbolSeparator = "_";
            MarketSymbolIsUppercase = false;
        }

        #region PUBLIC API*********************************************
        //GetSymbolsMetadata
        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var currencyPairs = await OnGetMarketSymbolsAsync();
            return ParseMarket(currencyPairs);
        }

        //GetSymbols
        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            JArray resp = await this.MakeJsonRequestAsync<JArray>("/currencyPairs.do");
            CheckResponseToken(resp);
            return resp.ToObject<string[]>();
        }

        //GetTicker 
        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            //https://api.lbank.info/v1/ticker.do?symbol=eth_btc
            JToken resp = await this.MakeJsonRequestAsync<JToken>($"/ticker.do?symbol={symbol}");
            CheckResponseToken(resp);
            return ParseTicker(resp);
        }

        //GetTickers  4      
        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            //https://api.lbank.info/v1/ticker.do?symbol=all

            JToken resp = await MakeJsonRequestAsync<JToken>($"/ticker.do?symbol=all");

            CheckResponseToken(resp);

            return ParseTickers(resp);
        }


        //GetOrderBook 5      
        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {

            //https://api.lbank.info/v1/depth.do?symbol=eth_btc&size=60&merge=1

            maxCount = Math.Min(maxCount, ORDER_BOOK_MAX_SIZE);
            JToken resp = await this.MakeJsonRequestAsync<JToken>($"/depth.do?symbol={symbol}&size={maxCount}&merge=0");
            CheckResponseToken(resp);
            ExchangeOrderBook book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(resp, maxCount: maxCount);
            book.SequenceId = resp["timestamp"].ConvertInvariant<long>();
            return book;
        }

        //GetRecentTrades   6     
        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            //https://api.lbank.info/v1/trades.do?symbol=eth_btc&size=600
            JToken resp = await this.MakeJsonRequestAsync<JToken>($"/trades.do?symbol={symbol}&size={RECENT_TRADS_MAX_SIZE}");
            CheckResponseToken(resp);
            return ParseRecentTrades(resp, symbol);
        }

        //GetCandles   7
        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //Get http://api.lbank.info/v1/kline.do
            limit = limit ?? 100;
            DateTime fromDate = startDate ?? DateTime.UtcNow.AddDays(-1);
            string type = CryptoUtility.SecondsToPeriodString(periodSeconds);
            long timestamp = CryptoUtility.UnixTimestampFromDateTimeSeconds(fromDate).ConvertInvariant<long>();
            JToken resp = await MakeJsonRequestAsync<JToken>($"/kline.do?symbol={symbol}&size={limit}&type={type}&time={timestamp}");
            CheckResponseToken(resp);
            return ParseMarketCandle(resp);
        }
        #endregion


        #region PARSERS PublicAPI
        private List<ExchangeMarket> ParseMarket(IEnumerable<string> array)
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>(array.Count());

            foreach (string item in array)
            {
                string[] pair = item.ToUpperInvariant().Split(this.MarketSymbolSeparator[0]);

                if (pair.Length != 2)
                {
                    continue;
                }

                markets.Add(
                    new ExchangeMarket
                    {
                        MarketId = item,
                        MarketSymbol = item,
                        BaseCurrency = pair[0],
                        QuoteCurrency = pair[1],
                        IsActive = true,
                    });
            }

            return markets;
        }

        private List<KeyValuePair<string, ExchangeTicker>> ParseTickers(JToken obj)
        {
            List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();

            foreach (JObject token in obj)
            {
                string symbol = token["symbol"].ConvertInvariant<string>();

                ExchangeTicker ticker = ParseTicker(token);

                tickerList.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
            }

            return tickerList;
        }

        private ExchangeTicker ParseTicker(JToken resp)
        {
            string symbol = resp["symbol"].ConvertInvariant<string>();
            DateTime timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(resp["timestamp"].ConvertInvariant<long>());
            JToken obj = resp["ticker"];
            decimal volume = obj["vol"].ConvertInvariant<decimal>();

            ExchangeTicker ticker = new ExchangeTicker
            {
                Ask = obj["high"].ConvertInvariant<decimal>(),
                Bid = obj["low"].ConvertInvariant<decimal>(),
                Last = obj["latest"].ConvertInvariant<decimal>(),
                //PercentChange = obj["change"].ConvertInvariant<decimal>(),
                Volume = new ExchangeVolume
                {
                    BaseCurrencyVolume = volume,
                    BaseCurrency = symbol,
                    QuoteCurrencyVolume = volume * obj["latest"].ConvertInvariant<decimal>(),
                    QuoteCurrency = symbol,
                    Timestamp = timestamp
                }
            };

            return ticker;
        }

        private List<ExchangeTrade> ParseRecentTrades(JToken trades, string symbol)
        {
            List<ExchangeTrade> exTradeList = new List<ExchangeTrade>(trades.Count());

            foreach (JToken token in trades)
            {
                long ms = token["date_ms"].ConvertInvariant<long>();
                DateTime timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(ms);

                exTradeList.Add(
                    new ExchangeTrade
                    {
                        Id = token["tid"].ConvertInvariant<long>(),
                        Timestamp = timestamp,
                        Price = token["price"].ConvertInvariant<decimal>(),
                        Amount = token["amount"].ConvertInvariant<decimal>(),
                        IsBuy = token["type"].ToStringLowerInvariant() == "buy"
                    });

            }

            return exTradeList;
        }

        private List<MarketCandle> ParseMarketCandle(JToken array)
        {
            List<MarketCandle> candles = new List<MarketCandle>();

            foreach (JArray item in array)
            {
                MarketCandle candle = new MarketCandle
                {
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(item[0].ConvertInvariant<long>()),
                    OpenPrice = item[1].ConvertInvariant<decimal>(),
                    HighPrice = item[2].ConvertInvariant<decimal>(),
                    LowPrice = item[3].ConvertInvariant<decimal>(),
                    ClosePrice = item[4].ConvertInvariant<decimal>(),
                    BaseCurrencyVolume = item[5].ConvertInvariant<double>()
                };

                candles.Add(candle);
            }

            return candles;
        }
        #endregion


        #region TRADING API*********************************************      

        //GetAmounts  8
        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
            { "api_key", PublicApiKey.ToUnsecureString() }
            };
            JToken resp = await MakeJsonRequestAsync<JToken>("/user_info.do", null, (Dictionary<string, object>)payload, "POST");
            CheckResponseToken(resp);
            return ParseAmounts(resp, true);
        }

        //PlaceOrder   9
        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
           {
                { "amount", order.Amount },
                { "api_key", PublicApiKey.ToUnsecureString() },
                { "price", order.Price },
                { "symbol", order.MarketSymbol },
                { "type", order.IsBuy ? "buy" : "sell"}
           };

            JToken resp = await MakeJsonRequestAsync<JToken>("/create_order.do", null, payload, "POST");

            CheckResponseToken(resp);

            return ParsePlaceOrder(resp, payload);
        }

        //GetOpenOrderDetails  10
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol)
        {

            Dictionary<string, object> payload = new Dictionary<string, object>
                    {
                        { "api_key", PublicApiKey.ToUnsecureString() },
                        { "symbol", marketSymbol }
                    };

            JToken resp = await MakeJsonRequestAsync<JToken>("/orders_info_no_deal.do", null, payload, "POST");

            CheckResponseToken(resp);

            return ParseOrderList(resp, ExchangeAPIOrderResult.Pending);
        }

        //GetCompletedOrderDetails  11
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "api_key", PublicApiKey.ToUnsecureString() },
                { "symbol", marketSymbol }
            };

            JToken resp = await MakeJsonRequestAsync<JToken>("/orders_info_history.do", null, payload, "POST");
            CheckResponseToken(resp);
            return ParseOrderList(resp, ExchangeAPIOrderResult.Filled);

        }

        //CancelOrder   12
        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "api_key", PublicApiKey.ToUnsecureString() },
                { "order_id", orderId },
                { "symbol", symbol },
            };
            JToken resp = await MakeJsonRequestAsync<JToken>("/cancel_order.do", null, payload, "POST");
            CheckResponseToken(resp);
        }


        //GetOrderDetails   13
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "api_key", PublicApiKey.ToUnsecureString() },
                { "order_id", orderId },
                { "symbol", symbol }
            };

            JToken resp = await MakeJsonRequestAsync<JToken>("/orders_info.do", null, payload, "POST");
            CheckResponseToken(resp);
            var orderResultList = ParseOrderList(resp, ExchangeAPIOrderResult.Unknown);
            CheckResponseList(orderResultList, orderId);

            return orderResultList[0];
        }


        //Withdraw  14
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {

            if (string.IsNullOrWhiteSpace(withdrawalRequest.Currency))
            {
                throw new APIException("Symbol empty");
            }
            if (string.IsNullOrWhiteSpace(withdrawalRequest.Address))
            {
                throw new APIException("Address empty");
            }

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "account", withdrawalRequest.Address },
                { "amount", withdrawalRequest.Amount },
                { "api_key", PublicApiKey.ToUnsecureString() },
                { "assetCode", withdrawalRequest.Currency },
                { "fee", withdrawalRequest.TakeFeeFromAmount }
            };

            JObject resp = await MakeJsonRequestAsync<JObject>("/withdraw.do", null, payload, "POST");

            CheckResponseToken(resp);

            return ParseWithdrawalResponse(resp);
        }


        //Withdraws  15
        protected override Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(string currency)
        {
            throw new NotImplementedException();
            /*
            Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "api_key", PublicApiKey.ToUnsecureString() },
                    { "assetCode", currency },
                    { "status", 0 } // all
                };

            JObject resp = await MakeJsonRequestAsync<JObject>("/withdraws.do", null, payload, "POST");

            CheckResponseToken(resp);

            return ParseWithdrawListResponse(resp);
            */
        }



        #endregion

        #region PARSERS PrivateAPI
        private Dictionary<string, decimal> ParseAmounts(JToken obj, bool isAll)
        {
            Dictionary<string, decimal> balance = new Dictionary<string, decimal>();

            JToken freeAssets = obj["info"]["free"];

            foreach (JProperty item in freeAssets)
            {
                string symbol = item.Name.ToStringInvariant();
                decimal amount = item.Value.ConvertInvariant<decimal>();

                if (isAll)
                {
                    balance[symbol] = amount;
                }
                else
                {
                    if (amount > 0m)
                    {
                        balance[symbol] = amount;
                    }
                }
            }

            return balance;
        }
        private ExchangeOrderResult ParsePlaceOrder(JToken obj, Dictionary<string, object> payload)
        {

            ExchangeOrderResult orderResult = new ExchangeOrderResult
            {
                Amount = payload["amount"].ConvertInvariant<decimal>(),
                MarketSymbol = payload["symbol"].ToStringInvariant(),
                OrderId = obj["order_id"].ToStringInvariant(),
                IsBuy = payload["type"].ToString().Equals("buy"),
                Price = payload["price"].ConvertInvariant<decimal>(),
                OrderDate = DateTime.Now,
                Result = ExchangeAPIOrderResult.Pending
            };

            return orderResult;
        }
        private List<ExchangeOrderResult> ParseOrderList(JToken orderList, ExchangeAPIOrderResult status)
        {
            JToken orders = orderList["orders"];

            List<ExchangeOrderResult> orderResultList = new List<ExchangeOrderResult>();

            foreach (JToken order in orders)
            {
                ExchangeOrderResult orderResult = ParseOrder(order);

                if (orderResult.Result == status || status == ExchangeAPIOrderResult.Unknown) //ApiOrderResult.Unknown - any states
                {
                    orderResultList.Add(orderResult);
                }
            }

            return orderResultList;
        }
        private ExchangeOrderResult ParseOrder(JToken obj)
        {
            long ms = obj["create_time"].ConvertInvariant<long>();

            ExchangeOrderResult orderResult = new ExchangeOrderResult
            {
                Amount = obj["amount"].ConvertInvariant<decimal>(),
                MarketSymbol = obj["symbol"].ToStringInvariant(),
                OrderId = obj["order_id"].ToStringInvariant(),
                IsBuy = obj["type"].ToString().Equals("buy"),
                AveragePrice = obj["avg_price"].ConvertInvariant<decimal>(),
                Price = obj["price"].ConvertInvariant<decimal>(),
                AmountFilled = obj["deal_amount"].ConvertInvariant<decimal>(),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(ms),
                Result = GetApiOrderResultFrom(obj["status"].ConvertInvariant<int>())
            };

            return orderResult;
        }
        private ExchangeWithdrawalResponse ParseWithdrawalResponse(JToken obj)
        {
            long ms = obj["time"].ConvertInvariant<long>();

            return new ExchangeWithdrawalResponse
            {
                Id = obj["id"].ConvertInvariant<string>(),
                Success = obj["success"].ConvertInvariant<bool>()
            };
        }


        private List<ExchangeWithdrawalResponse> ParseWithdrawListResponse(JToken withdrawList)
        {
            List<ExchangeWithdrawalResponse> withdrawResponseList = new List<ExchangeWithdrawalResponse>();

            JToken withdraws = withdrawList["list"];

            foreach (JToken item in withdraws)
            {
                ExchangeWithdrawalResponse withdrawResponse = ParseWithdrawalResponse(item);
                withdrawResponseList.Add(withdrawResponse);
            }

            return withdrawResponseList;
        }
        #endregion

        #region HELPERS  
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload == null || request.Method == "GET")
            {
                return;
            }

            string secret = this.PrivateApiKey.ToUnsecureString();

            payload.Add("secret_key", secret);

            string body = CryptoUtility.GetFormForPayload(payload);
            string sign = CryptoUtility.MD5Sign(body, PrivateApiKey.ToUnsecureBytesUTF8());

            payload.Remove("secret_key");
            payload.Add("sign", sign);
            body = payload.GetFormForPayload();
            await CryptoUtility.WriteToRequestAsync(request, body);
        }

        /// <summary>
        /// -1: Revoked 
        /// 0: Unfilled (Pending)
        /// 1: partial deal
        /// 2: The complete deal (Filled)
        /// 4: Withdrawal process        
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private ExchangeAPIOrderResult GetApiOrderResultFrom(int status)
        {
            switch (status)
            {
                case -1:
                    return ExchangeAPIOrderResult.Canceled;

                case 0:
                    return ExchangeAPIOrderResult.Pending;

                case 1:
                    return ExchangeAPIOrderResult.FilledPartially;

                case 2:
                    return ExchangeAPIOrderResult.Filled;

                case 4:
                    return ExchangeAPIOrderResult.PendingCancel;

                default:
                    return ExchangeAPIOrderResult.Unknown;
            }
        }


        private void CheckResponseToken(JToken token, string orderId = null)
        {
            if (token == null || !token.HasValues)
            {
                throw new APIException("Missing response");
            }

            else if (!(token is JArray) && !token["result"].ConvertInvariant<bool>() && token["error_code"] != null)
            {
                int errorCode = token["error_code"].ConvertInvariant<int>();
                string errMsg = GetErrorMsg(errorCode);
                throw new APIException($"ErrorCode: {errorCode} {errMsg}");
            }

            if (orderId != null && token["order_id"].ConvertInvariant<string>() != orderId)
            {

                throw new APIException($"Response order_id mismatch with {orderId}");
            }
        }

        private void CheckResponseList(List<ExchangeOrderResult> orderResultList, string orderId)
        {
            if (orderResultList.Count == 0 || (orderResultList.Count > 0 && orderResultList[0].OrderId != orderId))
            {
                throw new APIException("Missing response");
            }
        }

        private static string GetErrorMsg(int errorCode)
        {
            string errMsg = "";

            switch (errorCode)
            {
                case 10000: errMsg = "Internal error"; break;
                case 10001: errMsg = "Required parameters cannot be empty"; break;
                case 10002: errMsg = "Verification failed"; break;
                case 10003: errMsg = "illegal parameters"; break;
                case 10004: errMsg = "User requests are too frequent"; break;
                case 10005: errMsg = "Key does not exist"; break;
                case 10006: errMsg = "User does not exist"; break;
                case 10007: errMsg = "Invalid signature"; break;
                case 10008: errMsg = "This currency pair does not support"; break;

                case 10009: errMsg = "Limit order can not be missing the order price and order quantity"; break;
                case 10010: errMsg = "Order price or order quantity must be greater than 0"; break;
                case 10013: errMsg = "Minimum trading amount less than position 0.001"; break;
                case 10014: errMsg = "Insufficient amount of account currency"; break;
                case 10015: errMsg = "Order type error"; break;
                case 10016: errMsg = "Account balance is insufficient"; break;
                case 10017: errMsg = "Server exception"; break;
                case 10018: errMsg = "The number of order inquiry cannot be greater than 50 and less than 1"; break;

                case 10019: errMsg = "The number of withdrawals cannot be greater than 3 and less than 1"; break;
                case 10020: errMsg = "Minimum trading amount less than the amount of 0.001"; break;
                case 10021: errMsg = "Minimum transaction amount less than the limit order transaction price 0.01"; break;
                case 10022: errMsg = "Insufficient key authority"; break;
                case 10023: errMsg = "Does not support market price trading"; break;
                case 10024: errMsg = "Users cannot trade the pair"; break;
                case 10025: errMsg = "Order has been dealt"; break;
                case 10026: errMsg = "Order has been revoked"; break;
                case 10027: errMsg = "Order is being revoked"; break;

                case 10100: errMsg = "No coin rights"; break;
                case 10101: errMsg = "The coin rate is wrong"; break;
                case 10102: errMsg = "The amount of the coin is less than the single minimum"; break;
                case 10103: errMsg = "The amount of the coin exceeds the daily limit"; break;
                case 10104: errMsg = "The order has been processed and cannot be revoked"; break;
                case 10105: errMsg = "The order has been cancelled"; break;

                default: errMsg = $"Unknown error code: {errorCode}"; break;
            }

            return errMsg;
        }

        #endregion
    }

    public partial class ExchangeName { public const string LBank = "LBank"; }
}