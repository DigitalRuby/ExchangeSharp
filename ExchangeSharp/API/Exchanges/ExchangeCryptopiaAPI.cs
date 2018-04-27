﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed class ExchangeCryptopiaAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.Cryptopia;
        public override string BaseUrl { get; set; } = "https://www.cryptopia.co.nz/api";
        public override string BaseUrlWebSocket { get; set; }

        public ExchangeCryptopiaAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixMillisecondsString;
        }

        #region ProcessRequest 

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace('/', '_').Replace('-', '_');
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            // Only Private APIs are POST and need Authorization
            if (CanMakeAuthenticatedRequest(payload) && request.Method == "POST")
            {
                string requestContentBase64String = string.Empty;
                string nonce = payload["nonce"] as string;
                payload.Remove("nonce");

                string jsonContent = GetJsonForPayload(payload);
                if (!String.IsNullOrEmpty(jsonContent))
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        requestContentBase64String = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(jsonContent)));
                    }
                }
                else request.ContentLength = 0;

                string baseSig = string.Concat(PublicApiKey.ToUnsecureString(), request.Method, Uri.EscapeDataString(request.RequestUri.AbsoluteUri).ToLower(), nonce, requestContentBase64String);
                string signature = CryptoUtility.SHA256SignBase64(baseSig, Convert.FromBase64String(PrivateApiKey.ToUnsecureString()));
                request.Headers.Add(HttpRequestHeader.Authorization, string.Format("amx {0}:{1}:{2}", PublicApiKey.ToUnsecureString(), signature, nonce));

                // Cryptopia is very picky on how the payload is passed. There might be a better way to do this, but this works...
                using (Stream stream = request.GetRequestStream())
                {
                    byte[] content = Encoding.UTF8.GetBytes(jsonContent);
                    stream.Write(content, 0, content.Length);
                    stream.Flush();
                    stream.Close();
                }
            }
        }

        #endregion

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            //[{"Id":1, "Name":"Bitcoin", "Symbol":"BTC", "Algorithm":"sha256" "WithdrawFee":0.00010000, "MinWithdraw":0.00040000, "MinBaseTrade":0.0, "IsTipEnabled":false, "MinTip":0.0, "DepositConfirmations":6, "Status":"Maintenance","StatusMessage":"Unable to sync network","ListingStatus": "Active" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/GetCurrencies");
            result = CheckError(result);
            foreach(JToken token in result)
            {
                ExchangeCurrency currency = new ExchangeCurrency()
                {
                    Name = token["Symbol"].ToStringInvariant(),
                    FullName = token["Name"].ToStringInvariant(),
                    MinConfirmations = token["DepositConfirmations"].ConvertInvariant<int>(),
                    Notes = token["StatusMessage"].ToStringInvariant(),
                    TxFee = token["WithdrawFee"].ConvertInvariant<decimal>()
                };
                if (token["ListingStatus"].ToStringInvariant().Equals("Active")) currency.IsEnabled = !token["Status"].ToStringInvariant().Equals("Maintenance");
                else currency.IsEnabled = false;

                currencies[token["Symbol"].ToStringInvariant()] = currency;
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTradePairs");
            result = CheckError(result);
            foreach (JToken token in result) symbols.Add(token["Label"].Value<string>());
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            //[{ "Id":104, "Label":"LTC/BTC", "Currency":"Litecoin", "Symbol":"LTC", "BaseCurrency":"Bitcoin", "BaseSymbol":"BTC", "Status":"OK", "StatusMessage":"", "TradeFee":"0.20000000", "MinimumTrade":"0.00000001, "MaximumTrade":"1000000000.00000000", "MinimumBaseTrade":"0.00000500", "MaximumBaseTrade":"1000000000.00000000", "MinimumPrice":"0.00000001", "MaximumPrice":"1000000000.00000000" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTradePairs");
            result = CheckError(result);
            foreach(JToken token in result)
            {
                markets.Add(new ExchangeMarket()
                {
                     MarketName = token["Label"].ToStringInvariant(),
                     BaseCurrency = token["BaseSymbol"].ToStringInvariant(),
                     MarketCurrency = token["Symbol"].ToStringInvariant(),
                     MaxTradeSize = token["MaximumTrade"].ConvertInvariant<decimal>(),
                     MaxPrice = token["MaximumPrice"].ConvertInvariant<decimal>(),
                     MinTradeSize = token["MinimumTrade"].ConvertInvariant<decimal>(),
                     MinPrice = token["MinimumPrice"].ConvertInvariant<decimal>(),
                     IsActive  = token["Status"].ToStringInvariant().Equals("OK")
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/GetMarket/" + NormalizeSymbol(symbol));
            result = CheckError(result);
            return ParseTicker(result);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken result = await MakeJsonRequestAsync<JToken>("/GetMarkets");
            result = CheckError(result);
            foreach (JToken token in result) tickers.Add(new KeyValuePair<string, ExchangeTicker>(token["Label"].ToStringInvariant(), ParseTicker(token)));
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            ExchangeOrderBook orders = new ExchangeOrderBook();
            // {"TradePairId":100,"Label":"DOT/BTC","Price":0.00000317,"Volume":333389.57231468,"Total":1.05684494}
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketOrders/" + NormalizeSymbol(symbol) + "/" + maxCount.ToString());
            token = CheckError(token);
            if (token.HasValues)
            {
                foreach (JToken order in token["Buy"]) orders.Bids.Add(new ExchangeOrderPrice() { Price = order.Value<decimal>("Price"), Amount = order.Value<decimal>("Volume") });
                foreach (JToken order in token["Sell"]) orders.Asks.Add(new ExchangeOrderPrice() { Price = order.Value<decimal>("Price"), Amount = order.Value<decimal>("Volume") });
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // [{ "TradePairId":100,"Label":"LTC/BTC","Type":"Sell","Price":0.00006000, "Amount":499.99640000,"Total":0.02999978,"Timestamp": 1418297368}, ...]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketHistory/" + NormalizeSymbol(symbol));      // Default is last 24 hours
            token = CheckError(token);
            foreach (JToken trade in token) trades.Add(ParseTrade(trade));
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            string hours = sinceDateTime == null ? "24" : ((DateTime.Now - sinceDateTime).Value.TotalHours).ToString();
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/GetMarketHistory/" + NormalizeSymbol(symbol) + "/" + hours);      
            token = CheckError(token);
            foreach (JToken trade in token) trades.Add(ParseTrade(trade));
            var rc = callback?.Invoke(trades);
            // should we loop here to get additional more recent trades after a delay? 
        }


        /// <summary>
        /// Cryptopia doesn't support GetCandles. It is possible to get all trades since startdate (filter by enddate if needed) and then aggregate into MarketCandles by periodSeconds 
        /// TODO: Aggregate Cryptopia Trades into Candles
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();

            var payload = GetNoncePayload();
            payload.Add("Currency", "");

            // [ { "CurrencyId":1,"Symbol":"BTC","Total":"10300","Available":"6700.00000000","Unconfirmed":"2.00000000","HeldForTrades":"3400,00000000","PendingWithdraw":"200.00000000", "Address":"4HMjBARzTNdUpXCYkZDTHq8vmJQkdxXyFg","BaseAddress": "ZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFg","Status":"OK", "StatusMessage":"" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetBalance", null, payload, "POST");
            token = CheckError(token);
            if (token.HasValues)
            {
                foreach (JToken currency in token)
                {
                    decimal amount = currency["Total"].ConvertInvariant<decimal>();
                    if (amount > 0) amounts.Add(token["Symbol"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();

            var payload = GetNoncePayload();
            payload.Add("Currency", "");

            // [ { "CurrencyId":1,"Symbol":"BTC","Total":"10300","Available":"6700.00000000","Unconfirmed":"2.00000000","HeldForTrades":"3400,00000000","PendingWithdraw":"200.00000000", "Address":"4HMjBARzTNdUpXCYkZDTHq8vmJQkdxXyFg","BaseAddress": "ZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFgZDTHq8vmJQkdxXyFg","Status":"OK", "StatusMessage":"" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetBalance", null, payload, "POST");
            token = CheckError(token);
            if (token.HasValues)
            {
                foreach (JToken currency in token)
                {
                    decimal amount = currency["Available"].ConvertInvariant<decimal>();
                    if (amount > 0) amounts.Add(token["Symbol"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = GetNoncePayload();
            if (!String.IsNullOrEmpty(symbol)) payload["Market"] = symbol;
            else payload["Market"] = string.Empty;

            // [ { "TradeId": 23467, "TradePairId": 100,"Market": "DOT/BTC","Type": "Buy","Rate": 0.00000034, "Amount": 145.98000000, "Total": "0.00004963", "Fee": "0.98760000", "TimeStamp":"2014-12-07T20:04:05.3947572" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetTradeHistory", null, payload, "POST");
            token = CheckError(token);
            foreach (JToken order in token)
            {
                orders.Add(new ExchangeOrderResult()
                {
                    OrderId = order["TradeId"].ConvertInvariant<int>().ToStringInvariant(),
                    Symbol = order["Market"].ToStringInvariant(),
                    Amount = order["Amount"].ConvertInvariant<decimal>(),
                    AmountFilled = order["Amount"].ConvertInvariant<decimal>(),       // It doesn't look like partial fills are supplied on closed orders
                    Price = order["Rate"].ConvertInvariant<decimal>(),
                    AveragePrice = order["Rate"].ConvertInvariant<decimal>(),
                    OrderDate = order["TimeStamp"].ConvertInvariant<DateTime>(),
                    IsBuy = order["Type"].ToStringInvariant().Equals("Buy"),
                    Fees = order["Fee"].ConvertInvariant<decimal>(),
                    Result = ExchangeAPIOrderResult.Filled
                });
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = GetNoncePayload();
            payload["Market"] = string.IsNullOrEmpty(symbol) ? string.Empty : symbol;

            //[ {"OrderId": 23467,"TradePairId": 100,"Market": "DOT/BTC","Type": "Buy","Rate": 0.00000034,"Amount": 145.98000000, "Total": "0.00004963", "Remaining": "23.98760000", "TimeStamp":"2014-12-07T20:04:05.3947572" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetOpenOrders", null, payload, "POST");
            token = CheckError(token);
            foreach (JToken data in token)
            {
                ExchangeOrderResult order = new ExchangeOrderResult()
                {
                    OrderId = data["OrderId"].ConvertInvariant<int>().ToStringInvariant(),
                    OrderDate = data.Value<DateTime>("TimeStamp"),
                    Symbol = data.Value<String>("Market"),
                    Amount = data["Amount"].ConvertInvariant<decimal>(),
                    Price = data["Rate"].ConvertInvariant<decimal>(),
                    IsBuy = data.Value<String>("Type") == "Buy"  
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
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId)
        {
            var orders = await GetCompletedOrderDetailsAsync();
            return orders.Where(o => o.OrderId == orderId).FirstOrDefault();
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            ExchangeOrderResult newOrder = new ExchangeOrderResult() { Result = ExchangeAPIOrderResult.Error };

            var payload = GetNoncePayload();
            payload["Market"] = order.Symbol;
            payload["Type"] = order.IsBuy ? "Buy" : "Sell";
            payload["Rate"] = order.Price;
            payload["Amount"] = order.Amount;

            // { "OrderId": 23467, "FilledOrders": [44310,44311] }  - They don't say what those FilledOrders are. It's possible they represent partially filled order ids for this orders. Don't know.
            JToken token = await MakeJsonRequestAsync<JToken>("/SubmitTrade", null, payload, "POST");
            token = CheckError(token);
            if (token.HasValues && token["OrderId"] != null)
            {
                newOrder.OrderId = token["OrderId"].ConvertInvariant<int>().ToStringInvariant();
                newOrder.Result = ExchangeAPIOrderResult.Pending;           // Might we change this depending on what the filled orders are?
            }
            return newOrder;
        }

        // This should have a return value for success
        protected override async Task OnCancelOrderAsync(string orderId)
        {
            var payload = GetNoncePayload();
            payload["Type"] = "Trade";          // Cancel All by Market is supported. Here we're canceling by single Id
            payload["OrderId"] = int.Parse(orderId);
            // { "Success":true, "Error":null, "Data": [44310,44311]  }
            JToken token = await MakeJsonRequestAsync<JToken>("/CancelTrade", null, payload, "POST");
            token = CheckError(token);
        }

        /// <summary>
        /// Cryptopia does support filtering by Transaction Type (deposits and withdraws), but here we're returning both. The Tx Type will be returned in the Message field
        /// By Symbol isn't supported, so we'll filter. Also, the default limit is 100 transactions, we could possibly increase this to support the extra data we have to return for Symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();
            var payload = GetNoncePayload();
            // Uncomment as desired
            //payload["Type"] = "Deposit";
            //payload["Type"] = "Withdraw";
            //payload["Count"] = 100;

            // [ {"Id": 23467,"Currency": "DOT", "TxId": "6ddbaca454c97ba4e8a87a1cb49fa5ceace80b89eaced84b46a8f52c2b8c8ca3", "Type": "Deposit", "Amount": 145.98000000, "Fee": "0.00000000", "Status": "Confirmed", "Confirmations": "20", "TimeStamp":"2014-12-07T20:04:05.3947572", "Address": "" }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/GetTransactions", null, payload, "POST");
            token = CheckError(token);
            foreach(JToken data in token)
            {
                if (data["Currency"].ToStringInvariant().Equals(symbol))
                {
                    ExchangeTransaction tx = new ExchangeTransaction()
                    {
                         Address = data["Address"].ToStringInvariant(),                 
                         Amount = data["Amount"].ConvertInvariant<decimal>(),
                         BlockchainTxId = data["TxId"].ToStringInvariant(),
                         Notes = data["Type"].ToStringInvariant(),
                         PaymentId = data["Id"].ConvertInvariant<int>().ToString(),
                         TimestampUTC = data["TimeStamp"].ConvertInvariant<DateTime>(),
                         Symbol = data["Currency"].ToStringInvariant(),
                         TxFee = data["Fee"].ConvertInvariant<decimal>()
                    };
                    // They may support more status types, but it's not documented
                    switch((string)data["Status"])
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

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {

            var payload = GetNoncePayload();
            payload["Currency"] = symbol;
            JToken token = await MakeJsonRequestAsync<JToken>("/GetDepositAddress", null, payload, "POST");
            token = CheckError(token);
            if (token["Address"] == null ) return null;
            return new ExchangeDepositDetails()
            {
                Symbol = symbol,
                Address = token["Address"].ToStringInvariant(),
                AddressTag = token["BaseAddress"].ToStringInvariant()
            };
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = false };

            var payload = GetNoncePayload();
            payload.Add("Currency", withdrawalRequest.Symbol);
            payload.Add("Address", withdrawalRequest.Address);
            if (!string.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload.Add("PaymentId", withdrawalRequest.AddressTag);
            payload.Add("Amount", withdrawalRequest.Amount);
            JToken token = await MakeJsonRequestAsync<JToken>("/SubmitWithdraw", null, payload, "POST");
            if (token["Success"].ConvertInvariant<bool>() == true)
            {
                response.Id = token["Data"].ConvertInvariant<int>().ToStringInvariant();
                response.Success = true;
            }
            return response;
        }


        #endregion

        #region Private Functions

        private JToken CheckError(JToken result)
        {
            if (result == null || !result.HasValues || (result["Success"] != null && result["Success"].Value<bool>() != true))
            {
                if (!result.HasValues) throw new APIException("Unknown Error");
                else throw new APIException((result["Error"] != null ? result["Error"].Value<string>() : "Unknown Error"));
            }
            return result["Data"];
        }

        private ExchangeTicker ParseTicker(JToken token)
        {
            // [{ "TradePairId":100,"Label":"LTC/BTC","AskPrice":0.00006000,"BidPrice":0.02000000,"Low":0.00006000,"High":0.00006000,"Volume":1000.05639978,"LastPrice":0.00006000,"BuyVolume":34455.678,"SellVolume":67003436.37658233,"Change":-400.00000000,"Open": 0.00000500,"Close": 0.00000600, "BaseVolume": 3.58675866,"BaseBuyVolume": 11.25364758, "BaseSellVolume": 3456.06746543 }, ... ]
            var symbols = token["Label"].ToStringInvariant().Split('/');
            ExchangeTicker ticker = new ExchangeTicker()
            {
                Id = token["TradePairId"].ToStringInvariant(),
                Ask = token["AskPrice"].ConvertInvariant<decimal>(),
                Bid = token["BidPrice"].ConvertInvariant<decimal>(),
                Last = token["LastPrice"].ConvertInvariant<decimal>(),
                // Since we're parsing a ticker for a market, we'll use the volume/baseVolume fields here and ignore the Buy/Sell Volumes
                // This is a quess as to ambiguous intent of these fields.
                Volume = new ExchangeVolume()
                {
                    PriceSymbol = symbols[0],
                    QuantitySymbol = symbols[1],
                    PriceAmount = token["Volume"].ConvertInvariant<decimal>(),
                    QuantityAmount = token["BaseVolume"].ConvertInvariant<decimal>(),
                    Timestamp = DateTime.UtcNow           // No TimeStamp is returned, but Now seems appropriate
                }
            };
            return ticker;
        }

        private ExchangeTrade ParseTrade(JToken token)
        {
            // [{ "TradePairId":100,"Label":"LTC/BTC","Type":"Sell","Price":0.00006000, "Amount":499.99640000,"Total":0.02999978,"Timestamp": 1418297368}, ...]
            ExchangeTrade trade = new ExchangeTrade()
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(token["Timestamp"].ConvertInvariant<long>()).DateTime,
                Amount = token["Amount"].ConvertInvariant<decimal>(),
                Price = token["Price"].ConvertInvariant<decimal>(),
                IsBuy = token["Type"].ToStringInvariant().Equals("Buy")
            };
            return trade;
        }

        #endregion

    }
}
