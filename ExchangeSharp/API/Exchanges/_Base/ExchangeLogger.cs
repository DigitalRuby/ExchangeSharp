﻿/*
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
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Logs data from an exchange
    /// </summary>
    public sealed class ExchangeLogger : IDisposable
    {
        private readonly AutoResetEvent cancelEvent = new AutoResetEvent(false);

        private BinaryWriter sysTimeWriter;
        private BinaryWriter tickerWriter;
        private BinaryWriter bookWriter;
        private BinaryWriter tradeWriter;

        HashSet<string> tradeIds = new HashSet<string>();
        HashSet<string> tradeIds2 = new HashSet<string>();

        private async Task LoggerThread()
        {
            while (IsRunningInBackground && !cancelEvent.WaitOne(Interval))
            {
                await UpdateAsync();
            }
            cancelEvent.Set();
            IsRunningInBackground = false;
        }

        private BinaryWriter CreateLogWriter(string path, bool compress)
        {
            Stream stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            if (compress)
            {
                stream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal, false);
            }
            return new BinaryWriter(stream);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API</param>
        /// <param name="marketSymbol">The symbol to log, i.e. btcusd</param>
        /// <param name="intervalSeconds">Interval in seconds between updates</param>
        /// <param name="path">The path to write the log files to</param>
        /// <param name="compress">Whether to compress the log files using gzip compression</param>
        public ExchangeLogger(IExchangeAPI api, string marketSymbol, float intervalSeconds, string path, bool compress = false)
        {
            string compressExtension = (compress ? ".gz" : string.Empty);
            API = api;
            MarketSymbol = marketSymbol;
            Interval = TimeSpan.FromSeconds(intervalSeconds);
            sysTimeWriter = CreateLogWriter(Path.Combine(path, api.Name + "_time.bin" + compressExtension), compress);
            tickerWriter = CreateLogWriter(Path.Combine(path, api.Name + "_ticker.bin" + compressExtension), compress);
            bookWriter = CreateLogWriter(Path.Combine(path, api.Name + "_book.bin" + compressExtension), compress);
            tradeWriter = CreateLogWriter(Path.Combine(path, api.Name + "_trades.bin" + compressExtension), compress);
        }

        /// <summary>
        /// Update the logger - you can call this periodically if you don't want to call Start to run the logger in a background thread.
        /// </summary>
        public async Task UpdateAsync()
        {
            ExchangeTrade[] newTrades;
            HashSet<string> tmpTradeIds;

            try
            {
                if (MarketSymbol == "*")
                {
                    // get all symbols
                    Tickers = (await API.GetTickersAsync()).ToArray();
                    tickerWriter.Write(Tickers.Count);
                    foreach (KeyValuePair<string, ExchangeTicker> ticker in Tickers)
                    {
                        tickerWriter.Write(ticker.Key);
                        ticker.Value.ToBinary(tickerWriter);
                    }
                }
                else
                {
                    // make API calls first, if they fail we will try again later
                    Tickers = new KeyValuePair<string, ExchangeTicker>[1] { new KeyValuePair<string, ExchangeTicker>(MarketSymbol, await API.GetTickerAsync(MarketSymbol)) };
                    OrderBook = await API.GetOrderBookAsync(MarketSymbol);
                    Trades = (await API.GetRecentTradesAsync(MarketSymbol)).OrderBy(t => t.Timestamp).ToArray();

                    // all API calls succeeded, we can write to files

                    // write system date / time
                    sysTimeWriter.Write(CryptoUtility.UtcNow.Ticks);

                    // write ticker
                    Tickers.First().Value.ToBinary(tickerWriter);

                    // write order book
                    OrderBook.ToBinary(bookWriter);

                    // new trades only
                    newTrades = Trades.Where(t => !tradeIds.Contains(t.Id)).ToArray();

                    // write new trades
                    tradeWriter.Write(newTrades.Length);
                    foreach (ExchangeTrade trade in newTrades)
                    {
                        trade.ToBinary(tradeWriter);
                    }

                    // track trade ids for the latest set of trades
                    foreach (ExchangeTrade trade in Trades)
                    {
                        tradeIds2.Add(trade.Id);
                    }
                    tmpTradeIds = tradeIds;
                    tradeIds = tradeIds2;
                    tradeIds2 = tmpTradeIds;
                    tradeIds2.Clear();
                }

                DataAvailable?.Invoke(this);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Start a background thread for the logger
        /// </summary>
        /// <returns>True if started, false if already running in which case nothing happens</returns>
        public bool Start()
        {
            if (!IsRunningInBackground)
            {
                IsRunningInBackground = true;
                Task.Factory.StartNew(LoggerThread);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stop the logger background thread if it is running and waits for completion. Does not close the logger files. The logger can be started again later.
        /// </summary>
        public void Stop()
        {
            if (IsRunningInBackground)
            {
                cancelEvent.Set();
                cancelEvent.WaitOne();
            }
        }

        /// <summary>
        /// Close the logger
        /// </summary>
        public void Dispose()
        {
            Stop();
            sysTimeWriter.Close();
            tickerWriter.Close();
            bookWriter.Close();
            tradeWriter.Close();
        }

        /// <summary>
        /// Open a log reader for a base path - will detect if there is a compressed version automatically
        /// </summary>
        /// <param name="basePath">Base path (i.e. logFile.bin)</param>
        /// <returns>BinaryReader</returns>
        public static BinaryReader OpenLogReader(string basePath)
        {
            if (File.Exists(basePath))
            {
                return new BinaryReader(File.OpenRead(basePath));
            }
            return new BinaryReader(new System.IO.Compression.GZipStream(File.OpenRead(basePath + ".gz"), System.IO.Compression.CompressionMode.Decompress, false));
        }

        /// <summary>
        /// Begins logging exchanges - writes errors to console. You should block the app using Console.ReadLine.
        /// </summary>
        /// <param name="path">Path to write files to</param>
        /// <param name="intervalSeconds">Interval in seconds in between each log calls for each exchange</param>
        /// <param name="terminateAction">Call this when the process is about to exit, like a WM_CLOSE message on Windows.</param>
        /// <param name="compress">Whether to compress the log files</param>
        /// <param name="exchangeNamesAndSymbols">Exchange names and symbols to log</param>
        public static void LogExchanges(string path, float intervalSeconds, out Action terminateAction, bool compress, params string[] exchangeNamesAndSymbols)
        {
            bool terminating = false;
            Action terminator = null;
            path = (string.IsNullOrWhiteSpace(path) ? "./" : path);
            Dictionary<ExchangeLogger, int> errors = new Dictionary<ExchangeLogger, int>();
            List<ExchangeLogger> loggers = new List<ExchangeLogger>();
            for (int i = 0; i < exchangeNamesAndSymbols.Length;)
            {
                loggers.Add(new ExchangeLogger(ExchangeAPI.GetExchangeAPI(exchangeNamesAndSymbols[i++]), exchangeNamesAndSymbols[i++], intervalSeconds, path, compress));
            };
            foreach (ExchangeLogger logger in loggers)
            {
                logger.Start();
                logger.Error += (log, ex) =>
                {
                    int errorCount;
                    lock (errors)
                    {
                        if (!errors.TryGetValue(log, out errorCount))
                        {
                            errorCount = 0;
                        }
                        errors[log] = ++errorCount;
                    }
                    Logger.Info("Errors for {0}: {1}", log.API.Name, errorCount);
                };
            }
            terminator = () =>
            {
                if (!terminating)
                {
                    terminating = true;
                    foreach (ExchangeLogger logger in loggers.ToArray())
                    {
                        logger.Stop();
                        logger.Dispose();
                    }
                    loggers.Clear();
                }
            };
            terminateAction = terminator;

            // make sure to close properly
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                terminator();
            };
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) =>
            {
                terminator();
            };
            Logger.Info("Loggers \"{0}\" started, press ENTER or CTRL-C to terminate.", string.Join(", ", loggers.Select(l => l.API.Name)));
        }

        /// <summary>
        /// Enumerate over a log file that contains multiple tickers per entry. The previous dictionary is not valid once the enumerator is moved.
        /// Multi ticker log format: [int32 count](count times:)[string key][exchange ticker]
        /// </summary>
        /// <param name="path">Path to read from</param>
        /// <returns>Enumerator returning the tickers for each entry</returns>
        public static IEnumerable<Dictionary<string, ExchangeTicker>> ReadMultiTickers(string path)
        {
            int count;
            Dictionary<string, ExchangeTicker> tickers = new Dictionary<string, ExchangeTicker>(StringComparer.OrdinalIgnoreCase);
            ExchangeTicker ticker;
            string key;
            using (BinaryReader tickerReader = ExchangeLogger.OpenLogReader(path))
            {
                while (true)
                {
                    try
                    {
                        tickers.Clear();
                        count = tickerReader.ReadInt32();
                        while (count-- > 0)
                        {
                            key = tickerReader.ReadString();
                            ticker = new ExchangeTicker();
                            ticker.FromBinary(tickerReader);
                            tickers[key] = ticker;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                    yield return tickers;
                }
            }
        }

        /// <summary>
        /// The exchange API being logged
        /// </summary>
        public IExchangeAPI API { get; private set; }

        /// <summary>
        /// The symbol being logged
        /// </summary>
        public string MarketSymbol { get; private set; }

        /// <summary>
        /// The interval in between log calls
        /// </summary>
        public TimeSpan Interval { get; private set; }

        /// <summary>
        /// Whether the logger is running
        /// </summary>
        public bool IsRunningInBackground { get; set; }

        /// <summary>
        /// Event that fires when there is an error
        /// </summary>
        public event Action<ExchangeLogger, Exception> Error;

        /// <summary>
        /// Event that fires when new log data is available
        /// </summary>
        public event Action<ExchangeLogger> DataAvailable;

        /// <summary>
        /// Latest tickers
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> Tickers { get; private set; }

        /// <summary>
        /// Latest order book
        /// </summary>
        public ExchangeOrderBook OrderBook { get; private set; }

        /// <summary>
        /// Latest trades
        /// </summary>
        public ExchangeTrade[] Trades { get; private set; }
    }
}

