/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

// if you can't use ASP.NET signalr nuget package, comment this out
#define HAS_SIGNALR

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public partial class ExchangeBittrexAPI
    {

#if HAS_SIGNALR

        public sealed class BittrexWebSocketManager : SignalrManager
        {
            public BittrexWebSocketManager() : base("https://socket.bittrex.com/signalr", "c2")
            {
                FunctionNamesToFullNames["uS"] = "SubscribeToSummaryDeltas";
                FunctionNamesToFullNames["uE"] = "SubscribeToExchangeDeltas";
            }

            /// <summary>
            /// Subscribe to all market summaries
            /// </summary>
            /// <param name="callback">Callback</param>
            /// <returns>IDisposable to close the socket</returns>
            public IWebSocket SubscribeToSummaryDeltas(Action<string> callback)
            {
                SignalrManager.SignalrSocketConnection conn = new SignalrManager.SignalrSocketConnection(this);
                Task.Run(async () => await conn.OpenAsync("uS", (s) =>
                {
                    callback(s);
                    return Task.CompletedTask;
                }));
                return conn;
            }

            /// <summary>
            /// Subscribe to order book updates
            /// </summary>
            /// <param name="callback">Callback</param>
            /// <param name="symbols">The ticker to subscribe to</param>
            /// <returns>IDisposable to close the socket</returns>
            public IWebSocket SubscribeToExchangeDeltas(Action<string> callback, params string[] symbols)
            {
                SignalrManager.SignalrSocketConnection conn = new SignalrManager.SignalrSocketConnection(this);
                List<object[]> paramList = new List<object[]>();
                foreach (string symbol in symbols)
                {
                    paramList.Add(new object[] { symbol });
                }
                Task.Run(async () => await conn.OpenAsync("uE", (s) =>
                {
                    callback(s);
                    return Task.CompletedTask;
                }, 0, paramList.ToArray()));
                return conn;
            }
        }

        private BittrexWebSocketManager webSocket;

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            void innerCallback(string json)
            {
                #region sample json
                /*
                {
                    Nonce : int,
                    Deltas : 
                    [
                        {
                            MarketName     : string,
                            High           : decimal,
                            Low            : decimal,
                            Volume         : decimal,
                            Last           : decimal,
                            BaseVolume     : decimal,
                            TimeStamp      : date,
                            Bid            : decimal,
                            Ask            : decimal,
                            OpenBuyOrders  : int,
                            OpenSellOrders : int,
                            PrevDay        : decimal,
                            Created        : date
                        }
                    ]
                }
                */
                #endregion

                var freshTickers = new Dictionary<string, ExchangeTicker>(StringComparer.OrdinalIgnoreCase);
                JToken token = JToken.Parse(json);
                token = token["D"];
                foreach (JToken ticker in token)
                {
                    string marketName = ticker["M"].ToStringInvariant();
                    decimal last = ticker["l"].ConvertInvariant<decimal>();
                    decimal ask = ticker["A"].ConvertInvariant<decimal>();
                    decimal bid = ticker["B"].ConvertInvariant<decimal>();
                    decimal volume = ticker["V"].ConvertInvariant<decimal>();
                    decimal baseVolume = ticker["m"].ConvertInvariant<decimal>();
                    DateTime timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(ticker["T"].ConvertInvariant<long>());
                    var t = new ExchangeTicker
                    {
                        Ask = ask,
                        Bid = bid,
                        Last = last,
                        Volume = new ExchangeVolume
                        {
                            ConvertedVolume = volume,
                            ConvertedSymbol = marketName,
                            BaseVolume = baseVolume,
                            BaseSymbol = marketName,
                            Timestamp = timestamp
                        }
                    };
                    freshTickers[marketName] = t;
                }
                callback(freshTickers);
            }
            var client = SocketManager;
            return client.SubscribeToSummaryDeltas(innerCallback);
        }

        protected override IWebSocket OnGetOrderBookDeltasWebSocket
        (
            Action<ExchangeOrderBook> callback,
            int maxCount = 20,
            params string[] symbols
        )
        {
            if (symbols == null || symbols.Length == 0)
            {
                symbols = GetSymbolsAsync().Sync().ToArray();
            }
            void innerCallback(string json)
            {
                #region sample json
                /*
                    {
                        MarketName : string,
                        Nonce      : int,
                        Buys: 
                        [
                            {
                                Type     : int,
                                Rate     : decimal,
                                Quantity : decimal
                            }
                        ],
                        Sells: 
                        [
                            {
                                Type     : int,
                                Rate     : decimal,
                                Quantity : decimal
                            }
                        ],
                        Fills: 
                        [
                            {
                                FillId    : int,
                                OrderType : string,
                                Rate      : decimal,
                                Quantity  : decimal,
                                TimeStamp : date
                            }
                        ]
                    }
                */
                #endregion

                var ordersUpdates = JsonConvert.DeserializeObject<BittrexStreamUpdateExchangeState>(json);
                var book = new ExchangeOrderBook();
                foreach (BittrexStreamOrderBookUpdateEntry ask in ordersUpdates.Sells)
                {
                    var depth = new ExchangeOrderPrice { Price = ask.Rate, Amount = ask.Quantity };
                    book.Asks[depth.Price] = depth;
                }

                foreach (BittrexStreamOrderBookUpdateEntry bid in ordersUpdates.Buys)
                {
                    var depth = new ExchangeOrderPrice { Price = bid.Rate, Amount = bid.Quantity };
                    book.Bids[depth.Price] = depth;
                }

                book.Symbol = ordersUpdates.MarketName;
                book.SequenceId = ordersUpdates.Nonce;
                callback(book);
            }

            return this.SocketManager.SubscribeToExchangeDeltas(innerCallback, symbols);
        }

        /// <summary>
        /// Gets the BittrexSocketClient for this API
        /// </summary>
        private BittrexWebSocketManager SocketManager
        {
            get
            {
                if (webSocket == null)
                {
                    lock (this)
                    {
                        if (webSocket == null)
                        {
                            webSocket = new BittrexWebSocketManager();
                        }
                    }
                }
                return webSocket;
            }
        }

#endif

        protected override void OnDispose()
        {

#if HAS_SIGNALR

            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;
            }

#endif

        }
    }
}
