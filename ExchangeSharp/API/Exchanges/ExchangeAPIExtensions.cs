/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp
{
    /// <summary>
    /// Contains useful extension methods for the ExchangeAPI
    /// </summary>
    public static class ExchangeAPIExtensions
    {
		/// <summary>
		/// Get full order book bids and asks via web socket. This is efficient and will only use the order book deltas.
		/// </summary>
		/// <param name="callback">Callback of symbol, order book</param>
		/// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
		/// <param name="symbol">Ticker symbols or null/empty for all of them (if supported)</param>
		/// <returns>Web socket, call Dispose to close</returns>
		public static IDisposable GetOrderBookWebSocket(this IExchangeAPI api, Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols)
		{
			// Gets a delta socket for a collection of order books, then maintains a full order book.
			// The suggested way to use this is:
			// 1. Open this socket and begin buffering events you receive
			// 2. Get a depth snapshot of the order books you care about
			// 3. Drop any event where SequenceNumber is less than or equal to the snapshot last update id
			// Notes:
			// * Confirm with the Exchange's API docs whether the data in each event is the absolute quantity or differential quantity
			// * If the quantity is 0, remove the price level
			// * Receiving an event that removes a price level that is not in your local order book can happen and is normal.
			// 

			Dictionary<string, ExchangeOrderBook> fullBooks = new Dictionary<string, ExchangeOrderBook>();
			void innerCallback(ExchangeOrderBook book)
			{
				// see if we have a full order book for the symbol
				if (!fullBooks.TryGetValue(book.Symbol, out ExchangeOrderBook fullBook))
				{
					fullBooks[book.Symbol] = fullBook = api.GetOrderBook(book.Symbol, 1000);
					fullBook.Symbol = book.Symbol;
				}

				// update deltas as long as the full book is at or before the delta timestamp
				if (fullBook.SequenceId <= book.SequenceId)
				{
					foreach (var ask in book.Asks)
					{
						if (ask.Value.Amount <= 0m || ask.Value.Price <= 0m)
						{
							fullBook.Asks.Remove(ask.Value.Price);
						}
						else
						{
							fullBook.Asks[ask.Value.Price] = ask.Value;
						}
					}
					foreach (var bid in book.Bids)
					{
						if (bid.Value.Amount <= 0m || bid.Value.Price <= 0m)
						{
							fullBook.Bids.Remove(bid.Value.Price);
						}
						else
						{
							fullBook.Bids[bid.Value.Price] = bid.Value;
						}
					}
					fullBook.SequenceId = book.SequenceId;
				}
				callback(fullBook);
            };

            return api.GetOrderBookDeltasWebSocket(innerCallback, maxCount, symbols);
        }
    }
}
