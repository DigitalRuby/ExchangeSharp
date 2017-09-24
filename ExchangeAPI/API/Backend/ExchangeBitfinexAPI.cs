/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public class ExchangeBitfinexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.bitfinex.com/v2";
        public string BaseUrlV1 { get; set; } = "https://api.bitfinex.com/v1";
        public override string Name => ExchangeAPI.ExchangeNameBitfinex;

        private string NormalizeSymbol(string symbol)
        {
            return symbol.ToUpperInvariant();
        }

        public override string[] GetSymbols()
        {
            return MakeJsonRequest<string[]>("/symbols", BaseUrlV1);
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            decimal[] ticker = MakeJsonRequest<decimal[]>("/ticker/t" + symbol);
            return new ExchangeTicker { Bid = ticker[0], Ask = ticker[2], Last = ticker[6], Volume = new ExchangeVolume { PriceAmount = ticker[7], PriceSymbol = symbol, QuantityAmount = ticker[7], QuantitySymbol = symbol, Timestamp = DateTime.UtcNow } };
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            const int maxCount = 100;
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/trades/t" + symbol.ToUpperInvariant() + "/hist?sort=" + (sinceDateTime == null ? "-1" : "1") + "&limit=" + maxCount;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            decimal[][] tradeChunk;
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&start=" + (long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value);
                }
                tradeChunk = MakeJsonRequest<decimal[][]>(url);
                if (tradeChunk == null || tradeChunk.Length == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((double)tradeChunk[tradeChunk.Length - 1][1]);
                }
                foreach (decimal[] tradeChunkPiece in tradeChunk)
                {
                    trades.Add(new ExchangeTrade { Amount = Math.Abs(tradeChunkPiece[2]), IsBuy = tradeChunkPiece[2] > 0m, Price = tradeChunkPiece[3], Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((double)tradeChunkPiece[1]), Id = (long)tradeChunkPiece[0] });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                foreach (ExchangeTrade t in trades)
                {
                    yield return t;
                }
                trades.Clear();
                if (tradeChunk.Length < 500 || sinceDateTime == null)
                {
                    break;
                }
                System.Threading.Thread.Sleep(5000);
            }
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            ExchangeOrderBook orders = new ExchangeOrderBook();
            decimal[][] books = MakeJsonRequest<decimal[][]>("/book/t" + symbol.ToUpperInvariant() + "/P0?len=" + maxCount);
            foreach (decimal[] book in books)
            {
                if (book[2] > 0m)
                {
                    orders.Bids.Add(new ExchangeOrderPrice { Amount = book[2], Price = book[0] });
                }
                else
                {
                    orders.Asks.Add(new ExchangeOrderPrice { Amount = -book[2], Price = book[0] });
                }
            }
            return orders;
        }
    }
}
