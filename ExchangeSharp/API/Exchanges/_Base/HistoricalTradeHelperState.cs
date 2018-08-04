/*
MIT LICENSE

Copyright 2018 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    internal class HistoricalTradeHelperState
    {
        private ExchangeAPI api;

        public Func<IEnumerable<ExchangeTrade>, bool> Callback { get; set; }
        public string Symbol { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Url { get; set; } // url with format [symbol], {0} = start timestamp, {1} = end timestamp
        public int DelayMilliseconds { get; set; } = 1000;
        public TimeSpan BlockTime { get; set; } = TimeSpan.FromHours(1.0); // how much time to move for each block of data, default 1 hour
        public bool MillisecondGranularity { get; set; } = true;
        public Func<DateTime, string> TimestampFunction { get; set; } // change date time to a url timestamp, use TimestampFunction or UrlFunction
        public Func<HistoricalTradeHelperState, string> UrlFunction { get; set; } // allows returning a custom url, use TimestampFunction or UrlFunction
        public Func<JToken, ExchangeTrade> ParseFunction { get; set; }
        public bool DirectionIsBackwards { get; set; } = true; // some exchanges support going from most recent to oldest, but others, like Gemini must go from oldest to newest

        public HistoricalTradeHelperState(ExchangeAPI api)
        {
            this.api = api;
        }

        public async Task ProcessHistoricalTrades()
        {
            if (this.Callback == null)
            {
                throw new ArgumentException("Missing required parameter", nameof(this.Callback));
            }
            else if (this.TimestampFunction == null && this.UrlFunction == null)
            {
                throw new ArgumentException("Missing required parameters", nameof(this.TimestampFunction) + "," + nameof(this.UrlFunction));
            }
            else if (this.ParseFunction == null)
            {
                throw new ArgumentException("Missing required parameter", nameof(this.ParseFunction));
            }
            else if (string.IsNullOrWhiteSpace(this.Url))
            {
                throw new ArgumentException("Missing required parameter", nameof(this.Url));
            }

            this.Symbol = this.api.NormalizeSymbol(this.Symbol);
            string url;
            this.Url = this.Url.Replace("[symbol]", this.Symbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            ExchangeTrade trade;
            this.EndDate = (this.EndDate ?? DateTime.UtcNow);
            this.StartDate = (this.StartDate ?? this.EndDate.Value.Subtract(this.BlockTime));
            string startTimestamp;
            string endTimestamp;
            HashSet<long> previousTrades = new HashSet<long>();
            HashSet<long> tempTradeIds = new HashSet<long>();
            HashSet<long> tmpIds;
            this.SetDates(out DateTime startDateMoving, out DateTime endDateMoving);

            while (true)
            {
                // format url and make request
                if (this.TimestampFunction != null)
                {
                    startTimestamp = this.TimestampFunction(startDateMoving);
                    endTimestamp = this.TimestampFunction(endDateMoving);
                    url = string.Format(this.Url, startTimestamp, endTimestamp);
                }
                else if (this.UrlFunction != null)
                {
                    url = this.UrlFunction(this);
                }
                else
                {
                    throw new InvalidOperationException("TimestampFunction or UrlFunction must be specified");
                }
                JToken obj = await this.api.MakeJsonRequestAsync<JToken>(url);

                // don't add this temp trade as it may be outside of the date/time range
                tempTradeIds.Clear();
                foreach (JToken token in obj)
                {
                    trade = this.ParseFunction(token);
                    if (!previousTrades.Contains(trade.Id) && trade.Timestamp >= this.StartDate.Value && trade.Timestamp <= this.EndDate.Value)
                    {
                        trades.Add(trade);
                    }
                    if (trade.Id != 0)
                    {
                        tempTradeIds.Add(trade.Id);
                    }
                }
                previousTrades.Clear();
                tmpIds = previousTrades;
                previousTrades = tempTradeIds;
                tempTradeIds = previousTrades;

                // set dates to next block
                if (trades.Count == 0)
                {
                    if (this.DirectionIsBackwards)
                    {
                        // no trades found, move the whole block back
                        endDateMoving = startDateMoving.Subtract(this.BlockTime);
                    }
                    else
                    {
                        // no trades found, move the whole block forward
                        startDateMoving = endDateMoving.Add(this.BlockTime);
                    }
                }
                else
                {
                    // sort trades in descending order and callback
                    if (this.DirectionIsBackwards)
                    {
                        trades.Sort((t1, t2) => t2.Timestamp.CompareTo(t1.Timestamp));
                    }
                    else
                    {
                        trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                    }
                    if (!this.Callback(trades))
                    {
                        break;
                    }

                    trade = trades[trades.Count - 1];
                    if (this.DirectionIsBackwards)
                    {
                        // set end date to the date of the earliest trade of the block, use for next request
                        if (this.MillisecondGranularity)
                        {
                            endDateMoving = trade.Timestamp.AddMilliseconds(-1.0);
                        }
                        else
                        {
                            endDateMoving = trade.Timestamp.AddSeconds(-1.0);
                        }
                        startDateMoving = endDateMoving.Subtract(this.BlockTime);
                    }
                    else
                    {
                        // set start date to the date of the latest trade of the block, use for next request
                        if (this.MillisecondGranularity)
                        {
                            startDateMoving = trade.Timestamp.AddMilliseconds(1.0);
                        }
                        else
                        {
                            startDateMoving = trade.Timestamp.AddSeconds(1.0);
                        }
                        endDateMoving = startDateMoving.Add(this.BlockTime);
                    }
                    trades.Clear();
                }
                // check for exit conditions
                if (this.DirectionIsBackwards)
                {
                    if (endDateMoving < this.StartDate.Value)
                    {
                        break;
                    }
                }
                else
                {
                    if (startDateMoving > this.EndDate.Value)
                    {
                        break;
                    }
                }
                this.ClampDates(ref startDateMoving, ref endDateMoving);
                await Task.Delay(this.DelayMilliseconds);
            }
        }

        private void SetDates(out DateTime startDateMoving, out DateTime endDateMoving)
        {
            if (this.DirectionIsBackwards)
            {
                endDateMoving = this.EndDate.Value;
                startDateMoving = endDateMoving.Subtract(this.BlockTime);
            }
            else
            {
                startDateMoving = this.StartDate.Value;
                endDateMoving = startDateMoving.Add(this.BlockTime);
            }
            this.ClampDates(ref startDateMoving, ref endDateMoving);
        }

        private void ClampDates(ref DateTime startDateMoving, ref DateTime endDateMoving)
        {
            if (this.DirectionIsBackwards)
            {
                if (startDateMoving < this.StartDate.Value)
                {
                    startDateMoving = this.StartDate.Value;
                }
            }
            else
            {
                if (endDateMoving > this.EndDate.Value)
                {
                    endDateMoving = this.EndDate.Value;
                }
            }
        }
    }
}