/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeCryptopiaAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.cryptopia.co.nz/api";

        public ExchangeCryptopiaAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixMillisecondsString;
            MarketSymbolSeparator = "/";
        }

        #region ProcessRequest

        public string NormalizeSymbolForUrl(string symbol)
        {
            return NormalizeMarketSymbol(symbol).Replace(MarketSymbolSeparator, "_");
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            // Only Private APIs are POST and need Authorization
            if (CanMakeAuthenticatedRequest(payload) && request.Method == "POST")
            {
                string requestContentBase64String = string.Empty;
                string nonce = payload["nonce"] as string;
                payload.Remove("nonce");

                string jsonContent = CryptoUtility.GetJsonForPayload(payload);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        requestContentBase64String = Convert.ToBase64String(md5.ComputeHash(jsonContent.ToBytesUTF8()));
                    }
                }

                string baseSig = string.Concat(PublicApiKey.ToUnsecureString(), request.Method, WebUtility.UrlEncode(request.RequestUri.AbsoluteUri).ToLowerInvariant(), nonce, requestContentBase64String);
                string signature = CryptoUtility.SHA256SignBase64(baseSig, Convert.FromBase64String(PrivateApiKey.ToUnsecureString()));
                request.AddHeader("authorization", string.Format("amx {0}:{1}:{2}", PublicApiKey.ToUnsecureString(), signature, nonce));

                // Cryptopia is very picky on how the payload is passed. There might be a better way to do this, but this works...
                byte[] content = jsonContent.ToBytesUTF8();
                await request.WriteAllAsync(content, 0, content.Length);
            }
        }

        #endregion ProcessRequest

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            //[{"Id":1, "Name":"Bitcoin", "Symbol":"BTC", "Algorithm":"sha256" "WithdrawFee":0.00010000, "MinWithdraw":0.00040000, "MinBaseTrade":0.0, "IsTipEnabled":false, "MinTip":0.0, "DepositConfirmations":6, "Status":"Maintenance","StatusMessage":"Unable to sync network","ListingStatus": "Active" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/GetCurrencies");
            foreach (JToken token in result)
            {
                ExchangeCurrency currency = new ExchangeCurrency()
                {
                    Name = token["Symbol"].ToStringInvariant(),
                    FullName = token["Name"].ToStringInvariant(),
                    MinConfirmations = token["DepositConfirmations"].ConvertInvariant<int>(),
                    Notes = token["StatusMessage"].ToStringInvariant(),
                    TxFee = token["WithdrawFee"].ConvertInvariant<decimal>(),
                    MinWithdrawalSize = token["MinWithdraw"].ConvertInvariant<decimal>()
                };

                bool enabled = !token["Status"].ToStringInvariant().Equals("Maintenance") && token["ListingStatus"].ToStringInvariant().Equals("Active");
                currency.DepositEnabled = enabled;
                currency.WithdrawalEnabled = enabled;

                currencies[token["Symbol"].ToStringInvariant()] = currency;
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTradePairs");
            foreach (JToken token in result)
            {
                symbols.Add(token["Label"].ToStringInvariant());
            }
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            //[{ "Id":104, "Label":"LTC/BTC", "Currency":"Litecoin", "Symbol":"LTC", "BaseCurrency":"Bitcoin", "BaseSymbol":"BTC", "Status":"OK", "StatusMessage":"", "TradeFee":"0.20000000", "MinimumTrade":"0.00000001, "MaximumTrade":"1000000000.00000000", "MinimumBaseTrade":"0.00000500", "MaximumBaseTrade":"1000000000.00000000", "MinimumPrice":"0.00000001", "MaximumPrice":"1000000000.00000000" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTradePairs");
            foreach (JToken token in result)
            {
                markets.Add(new ExchangeMarket()
                {
                    MarketId = token["Id"].ToStringInvariant(),
                    MarketSymbol = token["Label"].ToStringInvariant(),
                    //NOTE: Cryptopia is calls the QuoteCurrency the "BaseSymbol" and the BaseCurrency the "Symbol".. not confusing at all!
                    QuoteCurrency = token["BaseSymbol"].ToStringInvariant(),
                    BaseCurrency = token["Symbol"].ToStringInvariant(),
                    MaxTradeSize = token["MaximumTrade"].ConvertInvariant<decimal>(),
                    MaxTradeSizeInQuoteCurrency = token["MaximumBaseTrade"].ConvertInvariant<decimal>(),
                    MaxPrice = token["MaximumPrice"].ConvertInvariant<decimal>(),
                    MinTradeSize = token["MinimumTrade"].ConvertInvariant<decimal>(),
                    MinTradeSizeInQuoteCurrency = token["MinimumBaseTrade"].ConvertInvariant<decimal>(),
                    MinPrice = token["MinimumPrice"].ConvertInvariant<decimal>(),
                    IsActive = token["Status"].ToStringInvariant().Equals("OK")
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/GetMarket/" + NormalizeSymbolForUrl(marketSymbol));
            return ParseTicker(result);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken result = await MakeJsonRequestAsync<JToken>("/GetMarkets");
            foreach (JToken token in result) tickers.Add(new KeyValuePair<string, ExchangeTicker>(token["Label"].ToStringInvariant(), ParseTicker(token)));
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            // {"TradePairId":100,"Label":"DOT/BTC","Price":0.00000317,"Volume":333389.57231468,"Total":1.05684494}
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketOrders/" + NormalizeSymbolForUrl(marketSymbol) + "/" + maxCount.ToStringInvariant());
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(token, "Sell", "Buy", "Price", "Volume", maxCount: maxCount);
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // [{ "TradePairId":100,"Label":"LTC/BTC","Type":"Sell","Price":0.00006000, "Amount":499.99640000,"Total":0.02999978,"Timestamp": 1418297368}, ...]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketHistory/" + NormalizeSymbolForUrl(marketSymbol));      // Default is last 24 hours
            foreach (JToken trade in token) trades.Add(ParseTrade(trade));
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            string hours = startDate == null ? "24" : ((CryptoUtility.UtcNow - startDate.Value.ToUniversalTime()).TotalHours).ToStringInvariant();
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketHistory/" + NormalizeSymbolForUrl(marketSymbol) + "/" + hours);
            foreach (JToken trade in token) trades.Add(ParseTrade(trade));
            var rc = callback?.Invoke(trades);
            // should we loop here to get additional more recent trades after a delay?
        }

        /// <summary>
        /// Cryptopia doesn't support GetCandles. It is possible to get all trades since startdate (filter by enddate if needed) and then aggregate into MarketCandles by periodSeconds
        /// TODO: Aggregate Cryptopia Trades into Candles
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            throw new NotImplementedException();
        }

        #endregion Public APIs

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();

            var payload = await GetNoncePayloadAsync();
            payload.Add("Currency", "");

            // [ { "CurrencyId":1,"Symbol":"BTC","Total":"10300","Available":"6700.00000000","Unconfirmed":"2.00000000","HeldForTrades":"3400,00000000","PendingWithdraw":"200.00000000", "Address":"4HMjBARzTNdUpXCYkZDTHq8vmJQkdxXyFg","BaseAddress": "ZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFg","Status":"OK", "StatusMessage":"" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetBalance", null, payload, "POST");
            if (token.HasValues)
            {
                foreach (JToken currency in token)
                {
                    decimal amount = currency["Total"].ConvertInvariant<decimal>();
                    if (amount > 0) amounts.Add(currency["Symbol"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();

            var payload = await GetNoncePayloadAsync();
            payload.Add("Currency", "");

            // [ { "CurrencyId":1,"Symbol":"BTC","Total":"10300","Available":"6700.00000000","Unconfirmed":"2.00000000","HeldForTrades":"3400,00000000","PendingWithdraw":"200.00000000", "Address":"4HMjBARzTNdUpXCYkZDTHq8vmJQkdxXyFg","BaseAddress": "ZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFg","Status":"OK", "StatusMessage":"" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetBalance", null, payload, "POST");
            if (token.HasValues)
            {
                foreach (JToken currency in token)
                {
                    decimal amount = currency["Available"].ConvertInvariant<decimal>();
                    if (amount > 0) amounts.Add(currency["Symbol"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = await GetNoncePayloadAsync();
            if (marketSymbol.Length != 0)
            {
                payload["Market"] = marketSymbol;
            }
            else
            {
                payload["Market"] = string.Empty;
            }

            // [ { "TradeId": 23467, "TradePairId": 100,"Market": "DOT/BTC","Type": "Buy","Rate": 0.00000034, "Amount": 145.98000000, "Total": "0.00004963", "Fee": "0.98760000", "TimeStamp":"2014-12-07T20:04:05.3947572" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetTradeHistory", null, payload, "POST");
            foreach (JToken order in token)
            {
                orders.Add(new ExchangeOrderResult()
                {
                    OrderId = order["TradeId"].ToStringInvariant(),
                    MarketSymbol = order["Market"].ToStringInvariant(),
                    Amount = order["Amount"].ConvertInvariant<decimal>(),
                    AmountFilled = order["Amount"].ConvertInvariant<decimal>(),       // It doesn't look like partial fills are supplied on closed orders
                    Price = order["Rate"].ConvertInvariant<decimal>(),
                    AveragePrice = order["Rate"].ConvertInvariant<decimal>(),
                    OrderDate = order["TimeStamp"].ToDateTimeInvariant(),
                    IsBuy = order["Type"].ToStringInvariant().Equals("Buy"),
                    Fees = order["Fee"].ConvertInvariant<decimal>(),
                    Result = ExchangeAPIOrderResult.Filled
                });
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = await GetNoncePayloadAsync();
            payload["Market"] = string.IsNullOrEmpty(marketSymbol) ? string.Empty : NormalizeMarketSymbol(marketSymbol);

            //[ {"OrderId": 23467,"TradePairId": 100,"Market": "DOT/BTC","Type": "Buy","Rate": 0.00000034,"Amount": 145.98000000, "Total": "0.00004963", "Remaining": "23.98760000", "TimeStamp":"2014-12-07T20:04:05.3947572" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetOpenOrders", null, payload, "POST");
            foreach (JToken data in token)
            {
                ExchangeOrderResult order = new ExchangeOrderResult()
                {
                    OrderId = data["OrderId"].ToStringInvariant(),
                    OrderDate = data["TimeStamp"].ToDateTimeInvariant(),
                    MarketSymbol = data["Market"].ToStringInvariant(),
                    Amount = data["Amount"].ConvertInvariant<decimal>(),
                    Price = data["Rate"].ConvertInvariant<decimal>(),
                    IsBuy = data["Type"].ToStringInvariant() == "Buy"
                };
                order.AveragePrice = order.Price;
                order.AmountFilled = order.Amount - data["Remaining"].ConvertInvariant<decimal>();
                if (order.AmountFilled == 0m) order.Result = ExchangeAPIOrderResult.Pending;
                else if (order.AmountFilled < order.Amount) order.Result = ExchangeAPIOrderResult.FilledPartially;
                else if (order.Amount == order.AmountFilled) order.Result = ExchangeAPIOrderResult.Filled;
                else order.Result = ExchangeAPIOrderResult.Unknown;

                orders.Add(order);
            }
            return orders;
        }

        /// <summary>
        /// Not directly supported by Cryptopia, and this API call is ambiguous between open and closed orders, so we'll get all Closed orders and filter for OrderId
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            var orders = await GetCompletedOrderDetailsAsync(marketSymbol);
            return orders.Where(o => o.OrderId == orderId).FirstOrDefault();
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            ExchangeOrderResult newOrder = new ExchangeOrderResult() { Result = ExchangeAPIOrderResult.Error };

            var payload = await GetNoncePayloadAsync();
            payload["Market"] = order.MarketSymbol;
            payload["Type"] = order.IsBuy ? "Buy" : "Sell";
            payload["Rate"] = order.Price;
            payload["Amount"] = order.Amount;
            order.ExtraParameters.CopyTo(payload);

            // { "OrderId": 23467, "FilledOrders": [44310,44311] }  - They don't say what those FilledOrders are. It's possible they represent partially filled order ids for this orders. Don't know.
            JToken token = await MakeJsonRequestAsync<JToken>("/SubmitTrade", null, payload, "POST");
            if (token.HasValues && token["OrderId"] != null)
            {
                newOrder.OrderId = token["OrderId"].ToStringInvariant();
                newOrder.Result = ExchangeAPIOrderResult.Pending;           // Might we change this depending on what the filled orders are?
            }
            return newOrder;
        }

        // This should have a return value for success
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            var payload = await GetNoncePayloadAsync();
            payload["Type"] = "Trade";          // Cancel All by Market is supported. Here we're canceling by single Id
            payload["OrderId"] = orderId.ToStringInvariant();
            // { "Success":true, "Error":null, "Data": [44310,44311]  }
            await MakeJsonRequestAsync<JToken>("/CancelTrade", null, payload, "POST");
        }

        /// <summary>
        /// Cryptopia does support filtering by Transaction Type (deposits and withdraws), but here we're returning both. The Tx Type will be returned in the Message field
        /// By Symbol isn't supported, so we'll filter. Also, the default limit is 100 transactions, we could possibly increase this to support the extra data we have to return for Symbol
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();
            var payload = await GetNoncePayloadAsync();

            // Uncomment as desired
            //payload["Type"] = "Deposit";
            //payload["Type"] = "Withdraw";
            //payload["Count"] = 100;

            // [ {"Id": 23467,"Currency": "DOT", "TxId": "6ddbaca454c97ba4e8a87a1cb49fa5ceace80b89eaced84b46a8f52c2b8c8ca3", "Type": "Deposit", "Amount": 145.98000000, "Fee": "0.00000000", "Status": "Confirmed", "Confirmations": "20", "TimeStamp":"2014-12-07T20:04:05.3947572", "Address": "" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetTransactions", null, payload, "POST");
            foreach (JToken data in token)
            {
                if (data["Currency"].ToStringInvariant().Equals(currency))
                {
                    ExchangeTransaction tx = new ExchangeTransaction()
                    {
                        Address = data["Address"].ToStringInvariant(),
                        Amount = data["Amount"].ConvertInvariant<decimal>(),
                        BlockchainTxId = data["TxId"].ToStringInvariant(),
                        Notes = data["Type"].ToStringInvariant(),
                        PaymentId = data["Id"].ToStringInvariant(),
                        Timestamp = data["TimeStamp"].ToDateTimeInvariant(),
                        Currency = data["Currency"].ToStringInvariant(),
                        TxFee = data["Fee"].ConvertInvariant<decimal>()
                    };
                    // They may support more status types, but it's not documented
                    switch ((string)data["Status"])
                    {
                        case "Confirmed": tx.Status = TransactionStatus.Complete; break;
                        case "Pending": tx.Status = TransactionStatus.Processing; break;
                        default: tx.Status = TransactionStatus.Unknown; break;
                    }
                    deposits.Add(tx);
                }
            }
            return deposits;
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            var payload = await GetNoncePayloadAsync();
            payload["Currency"] = currency;
            JToken token = await MakeJsonRequestAsync<JToken>("/GetDepositAddress", null, payload, "POST");
            if (token["Address"] == null) return null;
            return new ExchangeDepositDetails()
            {
                Currency = currency,
                Address = token["Address"].ToStringInvariant(),
                AddressTag = token["BaseAddress"].ToStringInvariant()
            };
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = false };

            var payload = await GetNoncePayloadAsync();
            payload.Add("Currency", withdrawalRequest.Currency);
            payload.Add("Address", withdrawalRequest.Address);
            if (!string.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload.Add("PaymentId", withdrawalRequest.AddressTag);
            payload.Add("Amount", withdrawalRequest.Amount);
            JToken token = await MakeJsonRequestAsync<JToken>("/SubmitWithdraw", null, payload, "POST");
            response.Id = token.ConvertInvariant<int>().ToStringInvariant();
            response.Success = true;
            return response;
        }

        #endregion Private APIs

        #region Private Functions

        private ExchangeTicker ParseTicker(JToken token)
        {
            // [{ "TradePairId":100,"Label":"LTC/BTC","AskPrice":0.00006000,"BidPrice":0.02000000,"Low":0.00006000,"High":0.00006000,"Volume":1000.05639978,"LastPrice":0.00006000,"BuyVolume":34455.678,"SellVolume":67003436.37658233,"Change":-400.00000000,"Open": 0.00000500,"Close": 0.00000600, "BaseVolume": 3.58675866,"BaseBuyVolume": 11.25364758, "BaseSellVolume": 3456.06746543 }, ... ]
            string marketSymbol = token["Label"].ToStringInvariant();
            return this.ParseTicker(token, marketSymbol, "AskPrice", "BidPrice", "LastPrice", "Volume", "BaseVolume");
        }

        private ExchangeTrade ParseTrade(JToken token)
        {
            // [{ "TradePairId":100,"Label":"LTC/BTC","Type":"Sell","Price":0.00006000, "Amount":499.99640000,"Total":0.02999978,"Timestamp": 1418297368}, ...]
            return token.ParseTrade("Amount", "Price", "Type", "Timestamp", TimestampType.UnixSeconds, null);
        }

        #endregion Private Functions
    }

    public partial class ExchangeName { public const string Cryptopia = "Cryptopia"; }
}