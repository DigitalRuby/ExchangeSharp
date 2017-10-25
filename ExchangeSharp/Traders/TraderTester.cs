/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public unsafe class TraderTester : IDisposable
    {
        private readonly List<KeyValuePair<double, string>> csv = new List<KeyValuePair<double, string>>();
        //private double maxProfit;
        private TradeReaderMemory tradeReader;
        private DateTime startDate;
        private DateTime endDate;
        private const double InitialCashFlow = 25000.0;
        private const double UnitsToBuy = 1.0f;
        private long Interval = (long)TimeSpan.FromSeconds(15.0).TotalMilliseconds;
        //private bool staticTests;

        private void RunAllTests()
        {
            // Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, actions.ToArray());
        }

        private void RunStaticTests()
        {
            //staticTests = true;
        }

        public void Dispose()
        {
            tradeReader.Dispose();
        }

        public int Run(string[] args)
        {
            Stopwatch w = Stopwatch.StartNew();

            // bear market
            //startDate = new DateTime(2017, 6, 11, 17, 0, 0, DateTimeKind.Utc);
            //endDate = new DateTime(2017, 7, 18, 0, 0, 0, DateTimeKind.Utc);
            startDate = new DateTime(2017, 1, 3, 0, 0, 0, DateTimeKind.Utc);
            endDate = new DateTime(2017, 1, 7, 0, 0, 0, DateTimeKind.Utc);

            byte[] tradeData = TraderFileReader.GetBytesFromBinFiles(@"../../data/btcusd", startDate, endDate);
            tradeReader = new TradeReaderMemory(tradeData);

            if (csv.Count != 0)
            {
                using (StreamWriter csvWriter = new StreamWriter(@"../../data.csv"))
                {
                    csvWriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                        "StartCashFlow", "UnitsToBuy", "Interval", "BuyThresholdPercent", "SellThresholdPercent",
                        "BuyReverseThresholdPercent", "BuyFalseReverseThresholdPercent", "SellReverseThresholdPercent",
                        "Spend", "Profit", "SpendProfitDiff", "ItemCount", "Buys", "Sells", "CashFlow");
                    csv.Sort((k1, k2) => k2.Key.CompareTo(k1.Key));
                    Console.WriteLine("Max: {0}", csv[0].Value);
                    foreach (var kv in csv)
                    {
                        csvWriter.WriteLine(kv.Value);
                    }
                }
            }

            w.Stop();
            Console.WriteLine("Total time: {0}", w.Elapsed);
            return 0;
        }
    }
}
