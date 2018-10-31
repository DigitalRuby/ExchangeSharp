﻿/*
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
            /// <param name="marketSymbols">The market symbols to subscribe to</param>
            /// <returns>IDisposable to close the socket</returns>
            public IWebSocket SubscribeToExchangeDeltas(Action<string> callback, params string[] marketSymbols)
            {
                SignalrManager.SignalrSocketConnection conn = new SignalrManager.SignalrSocketConnection(this);
                List<object[]> paramList = new List<object[]>();
                foreach (string marketSymbol in marketSymbols)
                {
                    paramList.Add(new object[] { marketSymbol });
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

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] symbols)
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
                    var (baseCurrency, quoteCurrency) = ExchangeMarketSymbolToCurrencies(marketName);
                    decimal last = ticker["l"].ConvertInvariant<decimal>();
                    decimal ask = ticker["A"].ConvertInvariant<decimal>();
                    decimal bid = ticker["B"].ConvertInvariant<decimal>();
                    decimal baseCurrencyVolume = ticker["V"].ConvertInvariant<decimal>();
                    decimal quoteCurrencyVolume = ticker["m"].ConvertInvariant<decimal>();//NOTE: Bittrex uses the term BaseVolume when referring to QuoteCurrencyVolume
                    DateTime timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(ticker["T"].ConvertInvariant<long>());
                    var t = new ExchangeTicker
                    {
                        MarketSymbol = marketName,
                        Ask = ask,
                        Bid = bid,
                        Last = last,
                        Volume = new ExchangeVolume
                        {
                            BaseCurrencyVolume = baseCurrencyVolume,
                            BaseCurrency = baseCurrency,
                            QuoteCurrencyVolume = quoteCurrencyVolume,
                            QuoteCurrency = quoteCurrency,
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

        protected override IWebSocket OnGetOrderBookWebSocket
        (
            Action<ExchangeOrderBook> callback,
            int maxCount = 20,
            params string[] marketSymbols
        )
        {
            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
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

                book.MarketSymbol = ordersUpdates.MarketName;
                book.SequenceId = ordersUpdates.Nonce;
                callback(book);
            }

            return this.SocketManager.SubscribeToExchangeDeltas(innerCallback, marketSymbols);
        }

		protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
			}
			void innerCallback(string json)
			{
				var ordersUpdates = JsonConvert.DeserializeObject<BittrexStreamUpdateExchangeState>(json);
				foreach (var fill in ordersUpdates.Fills)
				{
					callback(new KeyValuePair<string, ExchangeTrade>(ordersUpdates.MarketName, new ExchangeTrade()
					{
						Amount = fill.Quantity,
						// Bittrex doesn't currently send out FillId on socket.bittrex.com, only beta.bittrex.com, but this will be ready when they start
						// https://github.com/Bittrex/beta/issues/2, https://github.com/Bittrex/bittrex.github.io/issues/3
						// You can always change the URL on the top of the file to beta.bittrex.com to start getting FillIds now
						Id = fill.FillId,
						IsBuy = fill.OrderSide == OrderSide.Buy,
						Price = fill.Rate,
						Timestamp = fill.Timestamp
					}));
				}
			}

			return this.SocketManager.SubscribeToExchangeDeltas(innerCallback, marketSymbols);
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
