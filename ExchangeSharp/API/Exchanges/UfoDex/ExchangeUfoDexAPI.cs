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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeUfoDexAPI : ExchangeAPI
    {
        // TODO: Set correct base url
        public override string BaseUrl { get; set; } = "https://ufodex.io/dexsrv/mainnet/api/v1";

        private ExchangeTicker ParseTicker(JToken token)
        {
            return new ExchangeTicker
            {
                // TODO: Parse out fields...
                // Ticker JSON { "GenTime":12345678901234 "Label":"UFO/BTC", "Ask":0.00000005, "Bid":0.00000003, "Open":0.00000006, "High":0.00000007, "Low":0.00000004, "Close":0.00000003, "Volume":3240956.04453450, "BaseVolume":455533325.98457433 }

                var symbols = token["Label"].ToStringInvariant().Split('/');

                Id      = token["GenTime"].ConvertInvariant<Timestamp>(), // ????
                Ask     = token["Ask"].ConvertInvariant<decimal>(),
                Bid     = token["Bid"].ConvertInvariant<decimal>(),
                Last    = token["Close"].ConvertInvariant<decimal>(),

                Volume = new ExchangeVolume() 
                {
                    PriceSymbol     = symbols[0],
                    QuantitySymbol  = symbols[1],
                    PriceAmount     = token["Volume"].ConvertInvariant<decimal>(),
                    QuantityAmount  = token["BaseVolume"].ConvertInvariant<decimal>(),
                    Timestamp       = token["GenTime"].ConvertInvariant<Timestamp>()
                }

            };
        }

        public ExchangeUfoDexAPI()
        {
            RequestContentType = "application/json";

            // TODO: Verify these are correct
            NonceStyle = NonceStyle.UnixMillisecondsString;
            MarketSymbolSeparator = "/";
            MarketSymbolIsUppercase = true;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            // TODO: Fix url
            // marketSymbol like "UFO/BTC"
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTicker/" + marketSymbol);
            return ParseTicker(result);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();

            // TODO: Fix url
            JToken result = await MakeJsonRequestAsync<JToken>("/GetTickers");
            foreach (JToken token in result)
            {
                // TODO: Get symbol from correct property name
                // {"UFO/BTC":{Ticker JSON}, "UFO/LTC":{Ticker JSON}, ...}
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(token["Symbol"].ToStringInvariant(), ParseTicker(token)));
            }
            return tickers;
        }
    }

    public partial class ExchangeName { public const string UfoDex = "UfoDex"; }
}