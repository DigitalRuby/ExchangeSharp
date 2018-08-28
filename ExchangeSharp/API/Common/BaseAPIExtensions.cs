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
using System.Net.WebSockets;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// BaseAPI extensions
    /// </summary>
    public static class BaseAPIExtensions
    {
        internal static void ParseVolumes(this JToken token, object baseVolumeKey, object convertVolumeKey, decimal last, out decimal baseVolume, out decimal convertVolume)
        {
            // parse out volumes, handle cases where one or both do not exist
            if (baseVolumeKey == null)
            {
                if (convertVolumeKey == null)
                {
                    baseVolume = convertVolume = 0m;
                }
                else
                {
                    convertVolume = token[convertVolumeKey].ConvertInvariant<decimal>();
                    baseVolume = (last <= 0m ? 0m : convertVolume / last);
                }
            }
            else
            {
                baseVolume = token[baseVolumeKey].ConvertInvariant<decimal>();
                if (convertVolumeKey == null)
                {
                    convertVolume = baseVolume * last;
                }
                else
                {
                    convertVolume = token[convertVolumeKey].ConvertInvariant<decimal>();
                }
            }
        }

        internal static MarketCandle ParseCandle(this BaseAPI api, JToken token, string symbol, int periodSeconds, object openKey, object highKey, object lowKey,
            object closeKey, object timestampKey, TimestampType timestampType, object baseVolumeKey, object convertVolumeKey = null, object weightedAverageKey = null)
        {
            MarketCandle candle = new MarketCandle
            {
                ClosePrice = token[closeKey].ConvertInvariant<decimal>(),
                ExchangeName = api.Name,
                HighPrice = token[highKey].ConvertInvariant<decimal>(),
                LowPrice = token[lowKey].ConvertInvariant<decimal>(),
                Name = symbol,
                OpenPrice = token[openKey].ConvertInvariant<decimal>(),
                PeriodSeconds = periodSeconds,
                Timestamp = CryptoUtility.ParseTimestamp(token[timestampKey], timestampType)
            };

            token.ParseVolumes(baseVolumeKey, convertVolumeKey, candle.ClosePrice, out decimal baseVolume, out decimal convertVolume);
            candle.BaseVolume = (double)baseVolume;
            candle.ConvertedVolume = (double)convertVolume;
            if (weightedAverageKey != null)
            {
                candle.WeightedAverage = token[weightedAverageKey].ConvertInvariant<decimal>();
            }
            return candle;
        }
    }
}
