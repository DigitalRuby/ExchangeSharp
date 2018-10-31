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
    /// <summary>
    /// Contains functions to query cryptowatch API
    /// </summary>
    public sealed class CryptowatchAPI : BaseAPI
    {
        public override string BaseUrl { get; set; } = "https://api.cryptowat.ch";

        private async Task<JToken> MakeCryptowatchRequestAsync(string subUrl)
        {
            return await MakeJsonRequestAsync<JToken>(subUrl);
        }

        /// <summary>
        /// Get market candles
        /// </summary>
        /// <param name="exchange">Exchange name</param>
        /// <param name="marketName">Market name</param>
        /// <param name="before">Optional date to restrict data to before this date</param>
        /// <param name="after">Optional date to restrict data to after this date</param>
        /// <param name="periods">Periods</param>
        /// <returns>Market candles</returns>
        public async Task<IEnumerable<MarketCandle>> GetMarketCandlesAsync(string exchange, string marketName, DateTime? before, DateTime? after, params int[] periods)
        {
            await new SynchronizationContextRemover();

            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = string.Join(",", periods);
            string beforeDateString = (before == null ? string.Empty : "&before=" + (long)before.Value.UnixTimestampFromDateTimeSeconds());
            string afterDateString = (after == null ? string.Empty : "&after=" + (long)after.Value.UnixTimestampFromDateTimeSeconds());
            string url = "/markets/" + exchange + "/" + marketName + "/ohlc?periods=" + periodString + beforeDateString + afterDateString;
            JToken token = await MakeCryptowatchRequestAsync(url);
            foreach (JProperty prop in token)
            {
                foreach (JToken candleToken in prop.Value)
                {
                    MarketCandle candle = this.ParseCandle(candleToken, marketName, 0, 1, 2, 3, 4, 0, TimestampType.UnixSeconds, 5);
                    candle.PeriodSeconds = prop.Name.ConvertInvariant<int>();
                    candles.Add(candle);
                }
            }

            return candles;
        }

        /// <summary>
        /// Retrieve all market summaries
        /// </summary>
        /// <returns>Market summaries</returns>
        public async Task<IEnumerable<MarketSummary>> GetMarketSummaries()
        {
            await new SynchronizationContextRemover();

            List<MarketSummary> summaries = new List<MarketSummary>();
            JToken token = await MakeCryptowatchRequestAsync("/markets/summaries");
            foreach (JProperty prop in token)
            {
                string[] pieces = prop.Name.Split(':');
                if (pieces.Length != 2)
                {
                    continue;
                }
                summaries.Add(new MarketSummary
                {
                    ExchangeName = pieces[0],
                    Name = pieces[1],
                    HighPrice = prop.Value["price"]["high"].ConvertInvariant<decimal>(),
                    LastPrice = prop.Value["price"]["last"].ConvertInvariant<decimal>(),
                    LowPrice = prop.Value["price"]["low"].ConvertInvariant<decimal>(),
                    PriceChangeAmount = prop.Value["price"]["change"]["absolute"].ConvertInvariant<decimal>(),
                    PriceChangePercent = prop.Value["price"]["change"]["percentage"].ConvertInvariant<float>(),
                    Volume = prop.Value["volume"].ConvertInvariant<double>()
                });
            }

            return summaries;
        }

        public async Task<ExchangeOrderBook> GetOrderBookAsync(string exchange, string marketSymbol, int maxCount = 100)
        {
            await new SynchronizationContextRemover();

            ExchangeOrderBook book = new ExchangeOrderBook();
            JToken result = await MakeJsonRequestAsync<JToken>("/markets/" + exchange.ToLowerInvariant() + "/" + marketSymbol + "/orderbook");
            int count = 0;
            foreach (JArray array in result["asks"])
            {
                if (++count > maxCount)
                    break;
                var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                book.Asks[depth.Price] = depth;
            }
            count = 0;
            foreach (JArray array in result["bids"])
            {
                if (++count > maxCount)
                    break;
                var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                book.Bids[depth.Price] = depth;
            }
            return book;
        }

    }
}
