/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public static class TraderExchangeExport
    {
        /// <summary>
        /// Export exchange data to csv and then to optimized bin files
        /// </summary>
        /// <param name="api">Exchange api, null to just convert existing csv files</param>
        /// <param name="symbol">Symbol to export</param>
        /// <param name="basePath">Base path to export to, should not contain symbol, symbol will be appended</param>
        /// <param name="sinceDateTime">Start date to begin export at</param>
        /// <param name="callback">Callback if api is not null to notify of progress</param>
        public static void ExportExchangeTrades(IExchangeAPI api, string symbol, string basePath, DateTime sinceDateTime, System.Action<long> callback = null)
        {
            basePath = Path.Combine(basePath, symbol);
            Directory.CreateDirectory(basePath);
            sinceDateTime = sinceDateTime.ToUniversalTime();
            if (api != null)
            {
                long count = 0;
                int lastYear = -1;
                int lastMonth = -1;
                StreamWriter writer = null;
                foreach (ExchangeTrade trade in api.GetHistoricalTrades(symbol, sinceDateTime))
                {
                    if (trade.Timestamp.Year != lastYear || trade.Timestamp.Month != lastMonth)
                    {
                        if (writer != null)
                        {
                            writer.Close();
                        }
                        lastYear = trade.Timestamp.Year;
                        lastMonth = trade.Timestamp.Month;
                        writer = new StreamWriter(basePath + trade.Timestamp.Year + "-" + trade.Timestamp.Month.ToString("00") + ".csv");
                    }
                    writer.WriteLine("{0},{1},{2}", CryptoUtility.UnixTimestampFromDateTimeSeconds(trade.Timestamp), trade.Price, trade.Amount);
                    if (++count % 100 == 0)
                    {
                        callback?.Invoke(count);
                    }
                }
                writer.Close();
                callback?.Invoke(count);
            }
            TraderFileReader.ConvertCSVFilesToBinFiles(basePath);
        }
    }
}
