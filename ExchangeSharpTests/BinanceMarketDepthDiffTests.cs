/*
MIT LICENSE

Copyright 2018 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharpTests
{
    using ExchangeSharp;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    [TestClass]
    public class BinanceMarketDepthDiffTests
    {
        [TestMethod]
        public void DeserializeDiff()
        {
            string toParse = @"{
  ""e"": ""depthUpdate"",
  ""E"": 123456789,    
  ""s"": ""BNBBTC"",     
  ""U"": 157,          
  ""u"": 160,          
  ""b"": [             
    [
      ""0.0024"",      
      ""10"",
      []             
    ]
  ],
  ""a"": [             
    [
      ""0.0026"",      
      ""100"",         
      []             
    ]
  ]
}";
            var diff = JsonConvert.DeserializeObject<BinanceMarketDepthDiffUpdate>(toParse);
            ValidateDiff(diff);
        }

        [TestMethod]
        public void DeserializeComboStream()
        {
            string toParse = @"{
	""stream"": ""bnbbtc@depth"",
	""data"": {
		""e"": ""depthUpdate"",
		""E"": 123456789,
		""s"": ""BNBBTC"",
		""U"": 157,
		""u"": 160,
		""b"": [
			[
				""0.0024"",
				""10"", []
			]
		],
		""a"": [
			[
				""0.0026"",
				""100"", []
			]
		]
	}
}";

            var multistream = JsonConvert.DeserializeObject<BinanceMultiDepthStream>(toParse);
            multistream.Stream.Should().Be("bnbbtc@depth");
            ValidateDiff(multistream.Data);
        }

        [TestMethod]
        public void DeserializeRealData()
        {
            string real = "{\"stream\":\"bnbbtc@depth\",\"data\":{\"e\":\"depthUpdate\",\"E\":1527540113575,\"s\":\"BNBBTC\",\"U\":77730662,\"u\":77730663,\"b\":[[\"0.00167300\",\"0.00000000\",[]],[\"0.00165310\",\"16.44000000\",[]]],\"a\":[]}}";
            var diff = JsonConvert.DeserializeObject<BinanceMultiDepthStream>(real);
        }

        private static void ValidateDiff(BinanceMarketDepthDiffUpdate diff)
        {
            diff.EventType.Should().Be("depthUpdate");
            diff.EventTime.Should().Be(123456789);
            diff.Symbol.Should().Be("BNBBTC");
            diff.FirstUpdate.Should().Be(157);
            diff.FinalUpdate.Should().Be(160);
            diff.Bids[0][0].Should().Be("0.0024");
            diff.Bids[0][1].Should().Be("10");
            diff.Asks[0][0].Should().Be("0.0026");
            diff.Asks[0][1].Should().Be("100");
        }
    }
}