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
    public class CryptowatchAPI : BaseAPI
    {
        public override string BaseUrl { get; set; } = "https://api.cryptowat.ch";
        public override string Name => "Cryptowatch";

        private JToken MakeCryptowatchRequest(string subUrl)
        {
            JToken token = MakeJsonRequest<JToken>(subUrl);
            if (token["result"] == null)
            {
                throw new APIException("Unexpected result from API");
            }
            return token["result"];
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
        public IEnumerable<MarketCandle> GetMarketCandles(string exchange, string marketName, DateTime? before, DateTime? after, params int[] periods)
        {
            string periodString = string.Join(",", periods);
            string beforeDateString = (before == null ? string.Empty : "&before=" + (long)before.Value.UnixTimestampFromDateTimeSeconds());
            string afterDateString = (after == null ? string.Empty : "&after=" + (long)after.Value.UnixTimestampFromDateTimeSeconds());
            string url = "/markets/" + exchange + "/" + marketName + "/ohlc?periods=" + periodString + beforeDateString + afterDateString;
            JToken token = MakeCryptowatchRequest(url);
            foreach (JProperty prop in token)
            {
                foreach (JArray array in prop.Value)
                {
                    yield return new MarketCandle
                    {
                        ExchangeName = exchange,
                        Name = marketName,
                        ClosePrice = array[4].ConvertInvariant<decimal>(),
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(array[0].ConvertInvariant<long>()),
                        HighPrice = array[2].ConvertInvariant<decimal>(),
                        LowPrice = array[3].ConvertInvariant<decimal>(),
                        OpenPrice = array[1].ConvertInvariant<decimal>(),
                        PeriodSeconds = prop.Name.ConvertInvariant<int>(),
                        VolumePrice = array[5].ConvertInvariant<double>(),
                        VolumeQuantity = array[5].ConvertInvariant<double>() * array[4].ConvertInvariant<double>()
                    };
                }
            }
        }

        /// <summary>
        /// Retrieve all market summaries
        /// </summary>
        /// <returns>Market summaries</returns>
        public IEnumerable<MarketSummary> GetMarketSummaries()
        {
            JToken token = MakeCryptowatchRequest("/markets/summaries");
            foreach (JProperty prop in token)
            {
                string[] pieces = prop.Name.Split(':');
                if (pieces.Length != 2)
                {
                    continue;
                }
                yield return new MarketSummary
                {
                    ExchangeName = pieces[0],
                    Name = pieces[1],
                    HighPrice = prop.Value["price"]["high"].ConvertInvariant<decimal>(),
                    LastPrice = prop.Value["price"]["last"].ConvertInvariant<decimal>(),
                    LowPrice = prop.Value["price"]["low"].ConvertInvariant<decimal>(),
                    PriceChangeAmount = prop.Value["price"]["change"]["absolute"].ConvertInvariant<decimal>(),
                    PriceChangePercent = prop.Value["price"]["change"]["percentage"].ConvertInvariant<float>(),
                    Volume = prop.Value["volume"].ConvertInvariant<double>()
                };
            }
        }
    }
}
