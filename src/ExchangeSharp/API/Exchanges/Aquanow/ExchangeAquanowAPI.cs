/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Diagnostics;

namespace ExchangeSharp
{
    using ExchangeSharp.Aquanow;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using System.IO;


    public sealed partial class ExchangeAquanowAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api-dev.aquanow.io";

        public string MarketUrl { get; set; } = "https://market.aquanow.io";
        public override string BaseUrlWebSocket { get; set; } = "wss://market-dev.aquanow.io/";

        public ExchangeAquanowAPI()
        {
            NonceStyle = NonceStyle.UnixMilliseconds;
            RequestContentType = "application/x-www-form-urlencoded";
            MarketSymbolSeparator = "-";
            MarketSymbolIsReversed = false;
            WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken token = await MakeJsonRequestAsync<JToken>("/availablesymbols", MarketUrl);
            foreach (string symbol in token)
            {
                symbols.Add(symbol);
            }
            return symbols;
        }


        // NOT SUPPORTED
        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken symbols = await MakeJsonRequestAsync<JToken>("/availablesymbols", MarketUrl);
            foreach (string symbol in symbols)
            {
                JToken bestPriceSymbol = await MakeJsonRequestAsync<JToken>($"/bestprice?symbol={symbol}", MarketUrl);
                decimal bid = bestPriceSymbol["bestBid"].ConvertInvariant<decimal>();
                decimal ask = bestPriceSymbol["bestAsk"].ConvertInvariant<decimal>();
                ExchangeTicker ticker = new ExchangeTicker { MarketSymbol = symbol, Bid = bid, Ask = ask };
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));

            }
            return tickers;
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {

            var currencies = new Dictionary<string, ExchangeCurrency>();
            var symbols = await GetMarketSymbolsAsync();
            foreach (string symbol in symbols)
            {
                var currency = new ExchangeCurrency
                {
                    Name = symbol
                };
                currencies[currency.Name] = currency;
            }

            return currencies;
        }

        protected override Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            return ConnectWebSocketAsync(string.Empty, (_socket, msg) =>
            {
                string message = msg.ToStringFromUTF8();
                var book = new ExchangeOrderBook();
                var snapshot = JsonConvert.DeserializeObject<Snapshot>(message);

                book.MarketSymbol = snapshot.symbol;
                foreach (responseFormat ask in snapshot.asks)
                {
                    decimal price = ask.quote;
                    decimal amount = ask.quantity;
                    book.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                }

                foreach (responseFormat bid in snapshot.bids)
                {
                    decimal price = bid.quote;
                    decimal amount = bid.quantity;
                    book.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                }
                callback(book);
                return Task.CompletedTask;
            }, async (_socket) =>
                {
                    if (marketSymbols == null || marketSymbols.Length == 0)
                    {
                        marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                    }
                    foreach (var sym in marketSymbols)
                    {
                        var subscribeRequest = new
                        {
                            type = "subscribe",
                            channel = "orderBook",
                            pair = sym
                        };
                        await _socket.SendMessageAsync(subscribeRequest);
                    }
                });
        }


        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {

            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("content-type", "application/json");
                var sigContent = new signatureContent { httpMethod = request.Method, path = request.RequestUri.LocalPath, nonce = payload["nonce"].ToStringInvariant() };
                string json = JsonConvert.SerializeObject(sigContent);
                string bodyRequest = JsonConvert.SerializeObject(payload);
                string hexSha384 = CryptoUtility.SHA384Sign(json, PrivateApiKey.ToUnsecureString());
                request.AddHeader("x-api-key", PublicApiKey.ToUnsecureString());
                request.AddHeader("x-signature", hexSha384);
                request.AddHeader("x-nonce", payload["nonce"].ToStringInvariant()
                );
                if (request.Method == "GET")
                {
                    await CryptoUtility.WriteToRequestAsync(request, null);
                }
                else
                {
                    await CryptoUtility.WriteToRequestAsync(request, bodyRequest);
                }
            }
        }
        // DONE 
        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            // In Aquanow market order, when buying crypto the amount of crypto that is bought is the receiveQuantity
            // and when selling the amount of crypto that is sold is the deliverQuantity
            string amountParameter = order.IsBuy ? "receiveQuantity" : "deliverQuantity";
            string amountReceived = order.IsBuy ? "deliverQuantity" : "receiveQuantity";
            string feesCurrency = order.IsBuy ? order.MarketSymbol.Substring(0, order.MarketSymbol.IndexOf('-')) : order.MarketSymbol.Substring(order.MarketSymbol.IndexOf('-') + 1);
            var payload = await GetNoncePayloadAsync();
            payload["ticker"] = order.MarketSymbol;
            payload["tradeSide"] = order.IsBuy ? "buy" : "sell";
            payload[amountParameter] = order.Amount;
            order.ExtraParameters.CopyTo(payload);
            JToken token = await MakeJsonRequestAsync<JToken>("/trades/v1/market", null, payload, "POST");
            // {
            //     "type": "marketOrderSubmitAck",
            //     "payload": {
            //         "orderId": "397de435-0352-4de3-a546-fec676ed10cd",
            //         "receiveCurrency": "BTC",
            //         "receiveQuantity": 0.0005,
            //         "deliverCurrency": "USD",
            //         "deliverQuantity": 4.607,
            //         "fee": 0.000001
            //     }
            // }
            var orderDetailsPayload = await GetNoncePayloadAsync();
            JToken result = await MakeJsonRequestAsync<JToken>($"/trades/v1/order?orderId={token["payload"]["orderId"].ToStringInvariant()}", null, orderDetailsPayload, "GET");
            ExchangeOrderResult orderDetails = new ExchangeOrderResult
            {
                OrderId = result["orderId"].ToStringInvariant(),
                AmountFilled = result["fillQtyQuote"].ToStringInvariant().ConvertInvariant<decimal>(),
                Amount = payload[amountParameter].ConvertInvariant<decimal>(),
                OrderDate = CryptoUtility.UtcNow,
                IsBuy = order.IsBuy,
                Fees = token["payload"]["fee"].ConvertInvariant<decimal>(),
                FeesCurrency = feesCurrency,
                MarketSymbol = order.MarketSymbol,
                Price = result["priceArrival"].ToStringInvariant().ConvertInvariant<decimal>(),
                AveragePrice = result["tradePriceAvg"].ToStringInvariant().ConvertInvariant<decimal>()

            };

            return orderDetails;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }
            var payload = await GetNoncePayloadAsync();
            JToken result = await MakeJsonRequestAsync<JToken>($"/trades/v1/order?orderId={orderId}", null, payload, "GET");
            bool isBuy = result["tradeSide"].ToStringInvariant() == "buy" ? true : false;
            ExchangeOrderResult orderDetails = new ExchangeOrderResult
            {
                OrderId = result["orderId"].ToStringInvariant(),
                AmountFilled = result["fillQtyQuote"].ToStringInvariant().ConvertInvariant<decimal>(),
                Amount = result["tradeSize"].ConvertInvariant<decimal>(),
                OrderDate = CryptoUtility.UtcNow,
                IsBuy = isBuy,
                Fees = result["fillFeeQuote"].ConvertInvariant<decimal>() + result["fillFeeQuotaAqua"].ConvertInvariant<decimal>(),
                FeesCurrency = result["quoteSymbol"].ToStringInvariant(),
                MarketSymbol = result["symbol"].ToStringInvariant(),
                Price = result["priceArrival"].ToStringInvariant().ConvertInvariant<decimal>(),
                AveragePrice = result["tradePriceAvg"].ToStringInvariant().ConvertInvariant<decimal>()

            };

            return orderDetails;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            var payload = await GetNoncePayloadAsync();
            payload["orderId"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/trades/v1/order", null, payload, "DELETE");
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public?command=returnOrderBook&currencyPair=all&depth=" + maxCount);
            foreach (JProperty token in obj.Children())
            {
                ExchangeOrderBook book = new ExchangeOrderBook();
                foreach (JArray array in token.First["asks"])
                {
                    var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                    book.Asks[depth.Price] = depth;
                }
                foreach (JArray array in token.First["bids"])
                {
                    var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                    book.Bids[depth.Price] = depth;
                }
                books.Add(new KeyValuePair<string, ExchangeOrderBook>(token.Name, book));
            }
            return books;
        }


    }

    public partial class ExchangeName { public const string Aquanow = "Aquanow"; }
}
