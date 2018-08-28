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
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>Contains useful extension methods and parsing for the ExchangeAPI classes</summary>
    public static class ExchangeAPIExtensions
    {
        /// <summary>Get full order book bids and asks via web socket. This is efficient and will
        /// only use the order book deltas (if supported by the exchange).</summary>
        /// <param name="callback">Callback containing full order book</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this
        /// parameter</param>
        /// <param name="symbols">Ticker symbols or null/empty for all of them (if supported)</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public static IWebSocket GetOrderBookWebSocket(this IExchangeAPI api, Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols)
        {
            // Notes:
            // * Confirm with the Exchange's API docs whether the data in each event is the absolute quantity or differential quantity
            // * Receiving an event that removes a price level that is not in your local order book can happen and is normal.
            var fullBooks = new ConcurrentDictionary<string, ExchangeOrderBook>();
            var partialOrderBookQueues = new Dictionary<string, Queue<ExchangeOrderBook>>();

            void applyDelta(SortedDictionary<decimal, ExchangeOrderPrice> deltaValues, SortedDictionary<decimal, ExchangeOrderPrice> bookToEdit)
            {
                foreach (ExchangeOrderPrice record in deltaValues.Values)
                {
                    if (record.Amount <= 0 || record.Price <= 0)
                    {
                        bookToEdit.Remove(record.Price);
                    }
                    else
                    {
                        bookToEdit[record.Price] = record;
                    }
                }
            }

            void updateOrderBook(ExchangeOrderBook fullOrderBook, ExchangeOrderBook freshBook)
            {
                lock (fullOrderBook)
                {
                    // update deltas as long as the full book is at or before the delta timestamp
                    if (fullOrderBook.SequenceId <= freshBook.SequenceId)
                    {
                        applyDelta(freshBook.Asks, fullOrderBook.Asks);
                        applyDelta(freshBook.Bids, fullOrderBook.Bids);
                        fullOrderBook.SequenceId = freshBook.SequenceId;
                    }
                }
            }

            async Task innerCallback(ExchangeOrderBook newOrderBook)
            {
                // depending on the exchange, newOrderBook may be a complete or partial order book
                // ideally all exchanges would send the full order book on first message, followed by delta order books
                // but this is not the case

                bool foundFullBook = fullBooks.TryGetValue(newOrderBook.Symbol, out ExchangeOrderBook fullOrderBook);
                switch (api.Name)
                {
                    // Fetch an initial book the first time and apply deltas on top
                    // send these exchanges scathing support tickets that they should send
                    // the full book for the first web socket callback message
                    case ExchangeName.Bittrex:
                    case ExchangeName.Binance:
                    case ExchangeName.Poloniex:
                    {
                        Queue<ExchangeOrderBook> partialOrderBookQueue;
                        bool requestFullOrderBook = false;

                        // attempt to find the right queue to put the partial order book in to be processed later
                        lock (partialOrderBookQueues)
                        {
                            if (!partialOrderBookQueues.TryGetValue(newOrderBook.Symbol, out partialOrderBookQueue))
                            {
                                // no queue found, make a new one
                                partialOrderBookQueues[newOrderBook.Symbol] = partialOrderBookQueue = new Queue<ExchangeOrderBook>();
                                requestFullOrderBook = !foundFullBook;
                            }

                            // always enqueue the partial order book, they get dequeued down below
                            partialOrderBookQueue.Enqueue(newOrderBook);
                        }

                        // request the entire order book if we need it
                        if (requestFullOrderBook)
                        {
                            fullOrderBook = await api.GetOrderBookAsync(newOrderBook.Symbol, maxCount);
                            fullOrderBook.Symbol = newOrderBook.Symbol;
                            fullBooks[newOrderBook.Symbol] = fullOrderBook;
                        }
                        else if (!foundFullBook)
                        {
                            // we got a partial book while the full order book was being requested
                            // return out, the full order book loop will process this item in the queue
                            return;
                        }
                        // else new partial book with full order book available, will get dequeued below

                        // check if any old books for this symbol, if so process them first
                        // lock dictionary of queues for lookup only
                        lock (partialOrderBookQueues)
                        {
                            partialOrderBookQueues.TryGetValue(newOrderBook.Symbol, out partialOrderBookQueue);
                        }

                        if (partialOrderBookQueue != null)
                        {
                            // lock the individual queue for processing, fifo queue
                            lock (partialOrderBookQueue)
                            {
                                while (partialOrderBookQueue.Count != 0)
                                {
                                    updateOrderBook(fullOrderBook, partialOrderBookQueue.Dequeue());
                                }
                            }
                        }
                        break;
                    }

                    // First response from exchange will be the full order book.
                    // Subsequent updates will be deltas, at least some exchanges have their heads on straight
                    case ExchangeName.BitMEX:
                    case ExchangeName.Okex:
                    case ExchangeName.Coinbase:
                    {
                        if (!foundFullBook)
                        {
                            fullBooks[newOrderBook.Symbol] = fullOrderBook = newOrderBook;
                        }
                        else
                        {
                            updateOrderBook(fullOrderBook, newOrderBook);
                        }
                        break;
                    }

                    // Websocket always returns full order book, WTF...?
                    case ExchangeName.Huobi:
                    {
                        fullBooks[newOrderBook.Symbol] = fullOrderBook = newOrderBook;
                        break;
                    }

                    default:
                        throw new NotSupportedException("Full order book web socket not supported for exchange " + api.Name);
                }

                fullOrderBook.LastUpdatedUtc = DateTime.UtcNow;
                callback(fullOrderBook);
            }

            IWebSocket socket = api.GetOrderBookDeltasWebSocket(async (b) =>
            {
                try
                {
                    await innerCallback(b);
                }
                catch
                {
                }
            }, maxCount, symbols);
            socket.Connected += (s) =>
            {
                // when we re-connect, we must invalidate the order books, who knows how long we were disconnected
                //  and how out of date the order books are
                fullBooks.Clear();
                lock (partialOrderBookQueues)
                {
                    partialOrderBookQueues.Clear();
                }
                return Task.CompletedTask;
            };
            return socket;
        }

        /// <summary>
        /// Place a limit order by first querying the order book and then placing the order for a threshold below the bid or above the ask that would fully fulfill the amount.
        /// The order book is scanned until an amount of bids or asks that will fulfill the order is found and then the order is placed at the lowest bid or highest ask price multiplied
        /// by priceThreshold.
        /// </summary>
        /// <param name="symbol">Symbol to sell</param>
        /// <param name="amount">Amount to sell</param>
        /// <param name="isBuy">True for buy, false for sell</param>
        /// <param name="orderBookCount">Amount of bids/asks to request in the order book</param>
        /// <param name="priceThreshold">Threshold below the lowest bid or above the highest ask to set the limit order price at. For buys, this is converted to 1 / priceThreshold.
        /// This can be set to 0 if you want to set the price like a market order.</param>
        /// <param name="thresholdToAbort">If the lowest bid/highest ask price divided by the highest bid/lowest ask price is below this threshold, throw an exception.
        /// This ensures that your order does not buy or sell at an extreme margin.</param>
        /// <param name="abortIfOrderBookTooSmall">Whether to abort if the order book does not have enough bids or ask amounts to fulfill the order.</param>
        /// <returns>Order result</returns>
        public static async Task<ExchangeOrderResult> PlaceSafeMarketOrderAsync(this ExchangeAPI api, string symbol, decimal amount, bool isBuy, int orderBookCount = 100, decimal priceThreshold = 0.9m,
            decimal thresholdToAbort = 0.75m, bool abortIfOrderBookTooSmall = false)
        {
            if (priceThreshold > 0.9m)
            {
                throw new APIException("You cannot specify a price threshold above 0.9m, otherwise there is a chance your order will never be fulfilled. For buys, this is " +
                    "converted to 1.0m / priceThreshold, so always specify the value below 0.9m");
            }
            else if (priceThreshold <= 0m)
            {
                priceThreshold = 1m;
            }
            else if (isBuy && priceThreshold > 0m)
            {
                priceThreshold = 1.0m / priceThreshold;
            }
            ExchangeOrderBook book = await api.GetOrderBookAsync(symbol, orderBookCount);
            if (book == null || (isBuy && book.Asks.Count == 0) || (!isBuy && book.Bids.Count == 0))
            {
                throw new APIException($"Error getting order book for {symbol}");
            }
            decimal counter = 0m;
            decimal highPrice = decimal.MinValue;
            decimal lowPrice = decimal.MaxValue;
            if (isBuy)
            {
                foreach (ExchangeOrderPrice ask in book.Asks.Values)
                {
                    counter += ask.Amount;
                    highPrice = Math.Max(highPrice, ask.Price);
                    lowPrice = Math.Min(lowPrice, ask.Price);
                    if (counter >= amount)
                    {
                        break;
                    }
                }
            }
            else
            {
                foreach (ExchangeOrderPrice bid in book.Bids.Values)
                {
                    counter += bid.Amount;
                    highPrice = Math.Max(highPrice, bid.Price);
                    lowPrice = Math.Min(lowPrice, bid.Price);
                    if (counter >= amount)
                    {
                        break;
                    }
                }
            }
            if (abortIfOrderBookTooSmall && counter < amount)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell") } order for {symbol} and amount {amount} cannot be fulfilled because the order book is too thin.");
            }
            else if (lowPrice / highPrice < thresholdToAbort)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell")} order for {symbol} and amount {amount} would place for a price below threshold of {thresholdToAbort}, aborting.");
            }
            ExchangeOrderRequest request = new ExchangeOrderRequest
            {
                Amount = amount,
                OrderType = OrderType.Limit,
                Price = CryptoUtility.RoundAmount((isBuy ? highPrice : lowPrice) * priceThreshold),
                ShouldRoundAmount = true,
                Symbol = symbol
            };
            ExchangeOrderResult result = await api.PlaceOrderAsync(request);

            // wait about 10 seconds until the order is fulfilled
            int i = 0;
            const int maxTries = 20; // 500 ms for each try
            for (; i < maxTries; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                result = await api.GetOrderDetailsAsync(result.OrderId, symbol);
                switch (result.Result)
                {
                    case ExchangeAPIOrderResult.Filled:
                    case ExchangeAPIOrderResult.Canceled:
                    case ExchangeAPIOrderResult.Error:
                        break;
                }
            }

            if (i == maxTries)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell")} order for {symbol} and amount {amount} timed out and may not have been fulfilled");
            }

            return result;
        }

        /// <summary>Common order book parsing method, most exchanges use "asks" and "bids" with
        /// arrays of length 2 for price and amount (or amount and price)</summary>
        /// <param name="token">Token</param>
        /// <param name="asks">Asks key</param>
        /// <param name="bids">Bids key</param>
        /// <param name="maxCount">Max count</param>
        /// <returns>Order book</returns>
        internal static ExchangeOrderBook ParseOrderBookFromJTokenArrays
        (
            this JToken token,
            string asks = "asks",
            string bids = "bids",
            string sequence = "ts",
            int maxCount = 100
        )
        {
            var book = new ExchangeOrderBook { SequenceId = token[sequence].ConvertInvariant<long>() };
            foreach (JArray array in token[asks])
            {
                var depth = new ExchangeOrderPrice { Price = array[0].ConvertInvariant<decimal>(), Amount = array[1].ConvertInvariant<decimal>() };
                book.Asks[depth.Price] = depth;
                if (book.Asks.Count == maxCount)
                {
                    break;
                }
            }

            foreach (JArray array in token[bids])
            {
                var depth = new ExchangeOrderPrice { Price = array[0].ConvertInvariant<decimal>(), Amount = array[1].ConvertInvariant<decimal>() };
                book.Bids[depth.Price] = depth;
                if (book.Bids.Count == maxCount)
                {
                    break;
                }
            }

            return book;
        }

        /// <summary>Common order book parsing method, checks for "amount" or "quantity" and "price"
        /// elements</summary>
        /// <param name="token">Token</param>
        /// <param name="asks">Asks key</param>
        /// <param name="bids">Bids key</param>
        /// <param name="price">Price key</param>
        /// <param name="amount">Quantity key</param>
        /// <param name="sequence">Sequence key</param>
        /// <param name="maxCount">Max count</param>
        /// <returns>Order book</returns>
        internal static ExchangeOrderBook ParseOrderBookFromJTokenDictionaries
        (
            this JToken token,
            string asks = "asks",
            string bids = "bids",
            string price = "price",
            string amount = "amount",
            string sequence = "ts",
            int maxCount = 100
        )
        {
            var book = new ExchangeOrderBook { SequenceId = token[sequence].ConvertInvariant<long>() };
            foreach (JToken ask in token[asks])
            {
                var depth = new ExchangeOrderPrice { Price = ask[price].ConvertInvariant<decimal>(), Amount = ask[amount].ConvertInvariant<decimal>() };
                book.Asks[depth.Price] = depth;
                if (book.Asks.Count == maxCount)
                {
                    break;
                }
            }

            foreach (JToken bid in token[bids])
            {
                var depth = new ExchangeOrderPrice { Price = bid[price].ConvertInvariant<decimal>(), Amount = bid[amount].ConvertInvariant<decimal>() };
                book.Bids[depth.Price] = depth;
                if (book.Bids.Count == maxCount)
                {
                    break;
                }
            }

            return book;
        }

        /// <summary>
        /// Parse a JToken into a ticker
        /// </summary>
        /// <param name="api">ExchangeAPI</param>
        /// <param name="token">Token</param>
        /// <param name="symbol">Symbol</param>
        /// <param name="askKey">Ask key</param>
        /// <param name="bidKey">Bid key</param>
        /// <param name="lastKey">Last key</param>
        /// <param name="baseVolumeKey">Base volume key</param>
        /// <param name="convertVolumeKey">Convert volume key</param>
        /// <param name="timestampKey">Timestamp key</param>
        /// <param name="timestampType">Timestamp type</param>
        /// <param name="baseCurrencyKey">Base currency key</param>
        /// <param name="convertCurrencyKey">Convert currency key</param>
        /// <param name="idKey">Id key</param>
        /// <returns>ExchangeTicker</returns>
        internal static ExchangeTicker ParseTicker(this ExchangeAPI api, JToken token, string symbol,
            object askKey, object bidKey, object lastKey, object baseVolumeKey,
            object convertVolumeKey = null, object timestampKey = null, TimestampType timestampType = TimestampType.None,
            object baseCurrencyKey = null, object convertCurrencyKey = null, object idKey = null)
        {
            if (token == null || !token.HasValues)
            {
                return null;
            }
            decimal last = token[lastKey].ConvertInvariant<decimal>();

            // parse out volumes, handle cases where one or both do not exist
            token.ParseVolumes(baseVolumeKey, convertVolumeKey, last, out decimal baseVolume, out decimal convertVolume);

            // pull out timestamp
            DateTime timestamp = (timestampKey == null ? DateTime.UtcNow : CryptoUtility.ParseTimestamp(token[timestampKey], timestampType));

            // split apart the symbol if we have a separator, otherwise just put the symbol for base and convert symbol
            string baseSymbol;
            string convertSymbol;
            if (baseCurrencyKey != null && convertCurrencyKey != null)
            {
                baseSymbol = token[baseCurrencyKey].ToStringInvariant();
                convertSymbol = token[convertCurrencyKey].ToStringInvariant();
            }
            else if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentNullException(nameof(symbol));
            }
            else if (api.SymbolSeparator.Length != 0)
            {
                string[] pieces = symbol.Split(api.SymbolSeparator[0]);
                if (pieces.Length != 2)
                {
                    throw new ArgumentException($"Symbol does not have the correct symbol separator of '{api.SymbolSeparator}'");
                }
                baseSymbol = pieces[0];
                convertSymbol = pieces[1];
            }
            else
            {
                baseSymbol = convertSymbol = symbol;
            }

            // create the ticker and return it
            JToken askValue = token[askKey];
            JToken bidValue = token[bidKey];
            if (askValue is JArray)
            {
                askValue = askValue[0];
            }
            if (bidValue is JArray)
            {
                bidValue = bidValue[0];
            }
            ExchangeTicker ticker = new ExchangeTicker
            {
                Ask = askValue.ConvertInvariant<decimal>(),
                Bid = bidValue.ConvertInvariant<decimal>(),
                Id = (idKey == null ? null : token[idKey].ToStringInvariant()),
                Last = last,
                Volume = new ExchangeVolume
                {
                    BaseVolume = baseVolume,
                    BaseSymbol = baseSymbol,
                    ConvertedVolume = convertVolume,
                    ConvertedSymbol = convertSymbol,
                    Timestamp = timestamp
                }
            };
            return ticker;
        }

        /// <summary>
        /// Parse a trade
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="amountKey">Amount key</param>
        /// <param name="priceKey">Price key</param>
        /// <param name="typeKey">Type key</param>
        /// <param name="timestampKey">Timestamp key</param>
        /// <param name="timestampType">Timestamp type</param>
        /// <param name="idKey">Id key</param>
        /// <param name="typeKeyIsBuyValue">Type key buy value</param>
        /// <returns>Trade</returns>
        internal static ExchangeTrade ParseTrade(this JToken token, object amountKey, object priceKey, object typeKey,
            object timestampKey, TimestampType timestampType, object idKey = null, string typeKeyIsBuyValue = "buy")
        {
            ExchangeTrade trade = new ExchangeTrade
            {
                Amount = token[amountKey].ConvertInvariant<decimal>(),
                Price = token[priceKey].ConvertInvariant<decimal>(),
                IsBuy = (token[typeKey].ToStringInvariant().EqualsWithOption(typeKeyIsBuyValue))
            };
            trade.Timestamp = (timestampKey == null ? DateTime.UtcNow : CryptoUtility.ParseTimestamp(token[timestampKey], timestampType));
            if (idKey == null)
            {
                trade.Id = trade.Timestamp.Ticks;
            }
            else
            {
                try
                {
                    trade.Id = (long)token[idKey].ConvertInvariant<ulong>();
                }
                catch
                {
                    // dont care
                }
            }
            return trade;
        }
    }
}