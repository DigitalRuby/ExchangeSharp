using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public class ExchangeLogger : IDisposable
    {
        private readonly AutoResetEvent cancelEvent = new AutoResetEvent(false);

        private BinaryWriter sysTimeWriter;
        private BinaryWriter tickerWriter;
        private BinaryWriter bookWriter;
        private BinaryWriter tradeWriter;

        HashSet<long> tradeIds = new HashSet<long>();
        HashSet<long> tradeIds2 = new HashSet<long>();

        private void LoggerThread()
        {
            while (IsRunningInBackground && !cancelEvent.WaitOne(Interval))
            {
                Update();
            }
            cancelEvent.Set();
            IsRunningInBackground = false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API</param>
        /// <param name="symbol">The symbol to log, i.e. btcusd</param>
        /// <param name="intervalSeconds">Interval in seconds between updates</param>
        /// <param name="path">The path to write the log files to</param>
        public ExchangeLogger(IExchangeAPI api, string symbol, float intervalSeconds, string path)
        {
            API = api;
            Symbol = symbol;
            Interval = TimeSpan.FromSeconds(intervalSeconds);
            sysTimeWriter = new BinaryWriter(File.Open(Path.Combine(path, api.Name + "_time.bin"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            tickerWriter = new BinaryWriter(File.Open(Path.Combine(path, api.Name + "_ticker.bin"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            bookWriter = new BinaryWriter(File.Open(Path.Combine(path, api.Name + "_book.bin"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            tradeWriter = new BinaryWriter(File.Open(Path.Combine(path, api.Name + "_trades.bin"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }

        /// <summary>
        /// Update the logger - you can call this periodically if you don't want to call Start to run the logger in a background thread.
        /// </summary>
        public void Update()
        {
            ExchangeTrade[] newTrades;
            HashSet<long> tmpTradeIds;

            try
            {
                if (Symbol == "*")
                {
                    // get all symbols
                    Tickers = API.GetTickers();
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
                    Tickers = new KeyValuePair<string, ExchangeTicker>[1] { new KeyValuePair<string, ExchangeTicker>(Symbol, API.GetTicker(Symbol)) };
                    OrderBook = API.GetOrderBook(Symbol);
                    Trades = API.GetRecentTrades(Symbol).OrderBy(t => t.Timestamp).ToArray();

                    // all API calls succeeded, we can write to files

                    // write system date / time
                    sysTimeWriter.Write(DateTime.UtcNow.Ticks);

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
        /// Begins logging exchanges - writes errors to console. You should block the app using Console.ReadLine.
        /// </summary>
        /// <param name="path">Path to write files to</param>
        /// <param name="intervalSeconds">Interval in seconds in between each log calls for each exchange</param>
        /// <param name="terminateAction">Call this when the process is about to exit, like a WM_CLOSE message on Windows.</param>
        /// <param name="exchangeNamesAndSymbols">Exchange names and symbols to log</param>
        public static void LogExchanges(string path, float intervalSeconds, out System.Action terminateAction, params string[] exchangeNamesAndSymbols)
        {
            bool terminating = false;
            System.Action terminator = null;
            path = (string.IsNullOrWhiteSpace(path) ? "./" : path);
            Dictionary<ExchangeLogger, int> errors = new Dictionary<ExchangeLogger, int>();
            List<ExchangeLogger> loggers = new List<ExchangeLogger>();
            for (int i = 0; i < exchangeNamesAndSymbols.Length;)
            {
                loggers.Add(new ExchangeLogger(ExchangeAPI.GetExchangeAPI(exchangeNamesAndSymbols[i++]), exchangeNamesAndSymbols[i++], intervalSeconds, path));
            };
            StreamWriter errorLog = File.CreateText(Path.Combine(path, "errors.txt"));
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
                    Console.WriteLine("Errors for {0}: {1}", log.API.Name, errorCount);
                    errorLog.WriteLine("Errors for {0}: {1}", log.API.Name, errorCount);
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
                    errorLog.Close();
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

            Console.WriteLine("Loggers \"{0}\" started, press ENTER or CTRL-C to terminate.", string.Join(", ", loggers.Select(l => l.API.Name)));
            errorLog.WriteLine("Loggers \"{0}\" started, press ENTER or CTRL-C to terminate.", string.Join(", ", loggers.Select(l => l.API.Name)));
        }

        /// <summary>
        /// The exchange API being logged
        /// </summary>
        public IExchangeAPI API { get; private set; }

        /// <summary>
        /// The symbol being logged
        /// </summary>
        public string Symbol { get; private set; }

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
        public event System.Action<ExchangeLogger, Exception> Error;

        /// <summary>
        /// Event that fires when new log data is available
        /// </summary>
        public event System.Action<ExchangeLogger> DataAvailable;

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

