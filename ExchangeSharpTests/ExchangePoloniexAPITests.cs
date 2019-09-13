/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ExchangeSharp;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NSubstitute;

namespace ExchangeSharpTests
{
    [TestClass]
    public sealed class ExchangePoloniexAPITests
    {
        private static ExchangePoloniexAPI CreatePoloniexAPI(string response = null)
        {
            var requestMaker = new MockAPIRequestMaker();
            if (response != null)
            {
                requestMaker.GlobalResponse = response;
            }
            var polo = new ExchangePoloniexAPI { RequestMaker = requestMaker };
            return polo;
        }

        [TestMethod]
        public void ParseBuyOrder_SingleTrade_HappyPath()
        {
            const string BuyOrder = @"{
    ""orderNumber"": 31226040,
    ""resultingTrades"": [{
        ""amount"": ""338.8732"",
        ""date"": ""2014-10-18 23:03:21"",
        ""rate"": ""0.00000173"",
        ""total"": ""0.00058625"",
        ""tradeID"": ""16164"",
        ""type"": ""buy""
    }]
}";

            var singleOrder = JsonConvert.DeserializeObject<JToken>(BuyOrder);
            var polo = CreatePoloniexAPI();
            ExchangeOrderResult order = polo.ParsePlacedOrder(singleOrder);
            order.OrderId.Should().Be("31226040");
            order.IsBuy.Should().BeTrue();
            order.Amount.Should().Be(338.8732m);
            order.OrderDate.Should().Be(new DateTime(2014, 10, 18, 23, 3, 21, DateTimeKind.Utc));
            order.AveragePrice.Should().Be(0.00000173m);
            order.AmountFilled.Should().Be(338.8732m);
            order.FeesCurrency.Should().BeNullOrEmpty();
            order.Fees.Should().Be(0);
        }

        [TestMethod]
        public void ParseSellOrder_SingleTrade_HappyPath()
        {
            const string SellOrder = @"{
    ""orderNumber"": 31226040,
    ""resultingTrades"": [{
        ""amount"": ""338.8732"",
        ""date"": ""2014-10-18 23:03:21"",
        ""rate"": ""0.00000173"",
        ""total"": ""0.00058625"",
        ""tradeID"": ""16164"",
        ""type"": ""sell""
    }]
}";

            var singleOrder = JsonConvert.DeserializeObject<JToken>(SellOrder);
            var polo = CreatePoloniexAPI();
            ExchangeOrderResult order = polo.ParsePlacedOrder(singleOrder);
            order.OrderId.Should().Be("31226040");
            order.IsBuy.Should().BeFalse();
            order.Amount.Should().Be(338.8732m);
            order.OrderDate.Should().Be(new DateTime(2014, 10, 18, 23, 3, 21));
            order.AveragePrice.Should().Be(0.00000173m);
            order.AmountFilled.Should().Be(338.8732m);
            order.FeesCurrency.Should().BeNullOrEmpty();
            order.Fees.Should().Be(0);
        }

        [TestMethod]
        public void ReturnOrderTrades_Sell_HasCorrectValues()
        {
            var order = new ExchangeOrderResult();
            var orderWithMultipleTrades = JsonConvert.DeserializeObject<JToken>(ReturnOrderTradesSell);
            var polo = CreatePoloniexAPI();
            polo.ParseOrderTrades(orderWithMultipleTrades, order);
            order.Amount.Should().Be(143.14m);
            order.AmountFilled.Should().Be(order.Amount);
            order.Fees.Should().Be(0.00006141m);
            order.FeesCurrency.Should().Be("BTC");
            order.IsBuy.Should().BeFalse();
            order.OrderId.Should().BeNullOrEmpty();
            order.AveragePrice.Should().Be(0.0001716132563851949140701411m);
            order.Price.Should().Be(order.AveragePrice);
            order.MarketSymbol.Should().Be("BTC_VIA");
        }

        [TestMethod]
        public void ReturnOrderTrades_Buy_HasCorrectValues()
        {
            var order = new ExchangeOrderResult();
            var orderWithMultipleTrades = JsonConvert.DeserializeObject<JToken>(ReturnOrderTrades_SimpleBuy);
            var polo = CreatePoloniexAPI();
            polo.ParseOrderTrades(orderWithMultipleTrades, order);
            order.OrderId.Should().BeNullOrEmpty();
            order.Amount.Should().Be(19096.46996880m);
            order.AmountFilled.Should().Be(order.Amount);
            order.IsBuy.Should().BeTrue();
            order.Fees.Should().Be(28.64470495m);
            order.FeesCurrency.Should().Be("XEM");
            order.MarketSymbol.Should().Be("BTC_XEM");
            order.Price.Should().Be(0.00005128m);
            order.AveragePrice.Should().Be(0.00005128m);
        }

        [TestMethod]
        public void ReturnOrderTrades_BuyComplicatedPriceAvg_IsCorrect()
        {
            var order = new ExchangeOrderResult();
            var orderWithMultipleTrades = JsonConvert.DeserializeObject<JToken>(ReturnOrderTrades_GasBuy);
            var polo = CreatePoloniexAPI();
            polo.ParseOrderTrades(orderWithMultipleTrades, order);
            order.AveragePrice.Should().Be(0.0397199908083616777777777778m);
            order.IsBuy.Should().BeTrue();
        }

        [TestMethod]
        public void ReturnOpenOrders_SingleMarket_Parses()
        {
            string marketOrdersJson = @"[{
            ""orderNumber"": ""120466"",
            ""type"": ""sell"",
            ""rate"": ""0.025"",
            ""amount"": ""100"",
            ""total"": ""2.5""
        }, {
            ""orderNumber"": ""120467"",
            ""type"": ""sell"",
            ""rate"": ""0.04"",
            ""amount"": ""100"",
            ""total"": ""4""
        }]";
            var polo = CreatePoloniexAPI();
            var marketOrders = JsonConvert.DeserializeObject<JToken>(marketOrdersJson);

            var orders = new List<ExchangeOrderResult>();

            if (marketOrders is JArray array)
            {
                foreach (JToken token in array)
                {
                    orders.Add(polo.ParseOpenOrder(token));
                }
            }

            orders[0].OrderId.Should().Be("120466");
            orders[0].IsBuy.Should().BeFalse();
            orders[0].Price.Should().Be(0.025m);
            orders[0].Result.Should().Be(ExchangeAPIOrderResult.Pending);
        }

        [TestMethod]
        public void ReturnOpenOrders_Unfilled_IsCorrect()
        {
            var polo = CreatePoloniexAPI();
            var marketOrders = JsonConvert.DeserializeObject<JToken>(Unfilled);
            ExchangeOrderResult order = polo.ParseOpenOrder(marketOrders[0]);
            order.OrderId.Should().Be("35329211614");
            order.IsBuy.Should().BeTrue();
            order.AmountFilled.Should().Be(0);
            order.Amount.Should().Be(0.01m);
            order.OrderDate.Should().Be(new DateTime(2018, 4, 6, 1, 3, 45, DateTimeKind.Utc));
            order.Fees.Should().Be(0);
            order.FeesCurrency.Should().BeNullOrEmpty();
            order.Result.Should().Be(ExchangeAPIOrderResult.Pending);
        }

        [TestMethod]
        public async Task GetOpenOrderDetails_Unfilled_IsCorrect()
        {
            var polo = CreatePoloniexAPI(Unfilled);

            IEnumerable<ExchangeOrderResult> orders = await polo.GetOpenOrderDetailsAsync("ETH_BCH");
            ExchangeOrderResult order = orders.Single();
            order.OrderId.Should().Be("35329211614");
            order.IsBuy.Should().BeTrue();
            order.AmountFilled.Should().Be(0);
            order.Amount.Should().Be(0.01m);
            order.OrderDate.Should().Be(new DateTime(2018, 4, 6, 1, 3, 45, DateTimeKind.Utc));
            order.Fees.Should().Be(0);
            order.FeesCurrency.Should().BeNullOrEmpty();
            order.Result.Should().Be(ExchangeAPIOrderResult.Pending);
        }

        [TestMethod]
        public async Task GetOpenOrderDetails_AllUnfilled_IsCorrect()
        {
            var polo = CreatePoloniexAPI(AllUnfilledOrders);

            IEnumerable<ExchangeOrderResult> orders = await polo.GetOpenOrderDetailsAsync(); // all
            ExchangeOrderResult order = orders.Single();
            order.OrderId.Should().Be("35329211614");
            order.IsBuy.Should().BeTrue();
            order.AmountFilled.Should().Be(0);
            order.Amount.Should().Be(0.01m);
            order.OrderDate.Should().Be(new DateTime(2018, 4, 6, 1, 3, 45, DateTimeKind.Utc));
            order.Fees.Should().Be(0);
            order.FeesCurrency.Should().BeNullOrEmpty();
            order.Result.Should().Be(ExchangeAPIOrderResult.Pending);
        }

        [TestMethod]
        public async Task GetOrderDetails_HappyPath()
        {
            var polo = CreatePoloniexAPI(ReturnOrderTrades_SimpleBuy);
            ExchangeOrderResult order = await polo.GetOrderDetailsAsync("1");

            order.OrderId.Should().Be("1");
            order.Amount.Should().Be(19096.46996880m);
            order.AmountFilled.Should().Be(order.Amount);
            order.IsBuy.Should().BeTrue();
            order.Fees.Should().Be(28.64470495m);
            order.FeesCurrency.Should().Be("XEM");
            order.MarketSymbol.Should().Be("BTC_XEM");
            order.Price.Should().Be(0.00005128m);
            order.AveragePrice.Should().Be(0.00005128m);
            order.Result.Should().Be(ExchangeAPIOrderResult.Filled);
        }

        [TestMethod]
        public void GetOrderDetails_OrderNotFound_DoesNotThrow()
        {
            const string response = @"{""error"":""Order not found, or you are not the person who placed it.""}";
            var polo = CreatePoloniexAPI(response);
            async Task a() => await polo.GetOrderDetailsAsync("1");
            Invoking(a).Should().Throw<APIException>();
        }

        [TestMethod]
        public void GetOrderDetails_OtherErrors_ThrowAPIException()
        {
            const string response = @"{""error"":""Big scary error.""}";
            var polo = CreatePoloniexAPI(response);

            async Task a() => await polo.GetOrderDetailsAsync("1");
            Invoking(a).Should().Throw<APIException>();
        }

        [TestMethod]
        public async Task GetCompletedOrderDetails_MultipleOrders()
        {
            var polo = CreatePoloniexAPI(ReturnOrderTrades_AllGas);
            IEnumerable<ExchangeOrderResult> orders = await polo.GetCompletedOrderDetailsAsync("ETH_GAS");
            orders.Should().HaveCount(2);
            ExchangeOrderResult sellorder = orders.Single(x => !x.IsBuy);
            sellorder.AveragePrice.Should().Be(0.04123m);
            sellorder.AmountFilled.Should().Be(9.71293428m);
            sellorder.FeesCurrency.Should().Be("ETH");
            sellorder.Fees.Should().Be(0.0006007m);
            sellorder.Result.Should().Be(ExchangeAPIOrderResult.Filled);

            ExchangeOrderResult buyOrder = orders.Single(x => x.IsBuy);
            buyOrder.AveragePrice.Should().Be(0.0397199908083616777777777778m);
            buyOrder.AmountFilled.Should().Be(18);
            buyOrder.FeesCurrency.Should().Be("GAS");
            buyOrder.Fees.Should().Be(0.0352725m);
            buyOrder.Result.Should().Be(ExchangeAPIOrderResult.Filled);
        }

        [TestMethod]
        public async Task GetCompletedOrderDetails_AllSymbols()
        {
            // {"BTC_MAID": [ { "globalTradeID": 29251512, "tradeID": "1385888", "date": "2016-05-03 01:29:55", "rate": "0.00014243", "amount": "353.74692925", "total": "0.05038417", "fee": "0.00200000", "orderNumber": "12603322113", "type": "buy", "category": "settlement" }, { "globalTradeID": 29251511, "tradeID": "1385887", "date": "2016-05-03 01:29:55", "rate": "0.00014111", "amount": "311.24262497", "total": "0.04391944", "fee": "0.00200000", "orderNumber": "12603319116", "type": "sell", "category": "marginTrade" }
            var polo = CreatePoloniexAPI(GetCompletedOrderDetails_AllSymbolsOrders);
            ExchangeOrderResult order = (await polo.GetCompletedOrderDetailsAsync()).First();
            order.MarketSymbol.Should().Be("BTC_MAID");
            order.OrderId.Should().Be("12603322113");
            order.OrderDate.Should().Be(new DateTime(2016, 5, 3, 1, 29, 55));
            order.AveragePrice.Should().Be(0.00014243m);
            order.Price.Should().Be(0.00014243m);
            order.Amount.Should().Be(353.74692925m);
            order.Fees.Should().Be(0.70749386m);
            order.IsBuy.Should().Be(true);
        }

        [TestMethod]
        public async Task OnGetDepositHistory_DoesNotFailOnMinTimestamp()
        {
            var polo = CreatePoloniexAPI(null);
            try
            {
                await polo.GetDepositHistoryAsync("doesntmatter");
            }
            catch (APIException ex)
            {
                Assert.IsTrue(ex.Message.Contains("No result"));
                return;
            }
            Assert.Fail("Expected APIException with message containing 'No result'");
        }

        [TestMethod]
        public async Task GetExchangeMarketFromCache_SymbolsMetadataCacheRefreshesWhenSymbolNotFound()
        {
            var polo = CreatePoloniexAPI(Resources.PoloniexGetSymbolsMetadata1);
            int requestCount = 0;
            polo.RequestMaker.RequestStateChanged = (r, s, o) =>
            {
                if (s == RequestMakerState.Begin)
                {
                    requestCount++;
                }
            };

            // retrieve without BTC_BCH in the result
            (await polo.GetExchangeMarketFromCacheAsync("XMR_LTC")).Should().NotBeNull();
            requestCount.Should().Be(1);
            (await polo.GetExchangeMarketFromCacheAsync("BTC_BCH")).Should().BeNull();
            requestCount.Should().Be(2);

            // now many moons later we request BTC_BCH, which wasn't in the first request but is in the latest exchange result
            (polo.RequestMaker as MockAPIRequestMaker).GlobalResponse = Resources.PoloniexGetSymbolsMetadata2;
            (await polo.GetExchangeMarketFromCacheAsync("BTC_BCH")).Should().NotBeNull();
            requestCount.Should().Be(3);

            // and lets make sure it doesn't return something for null and garbage symbols
            (await polo.GetExchangeMarketFromCacheAsync(null)).Should().BeNull();
            (await polo.GetExchangeMarketFromCacheAsync(string.Empty)).Should().BeNull();
            (await polo.GetExchangeMarketFromCacheAsync("324235!@^%Q@#%^")).Should().BeNull();
            (await polo.GetExchangeMarketFromCacheAsync("NOCOIN_NORESULT")).Should().BeNull();
        }

        private static Func<Task> Invoking(Func<Task> action) => action;

        #region RealResponseJSON
        private const string SingleMarketTradeHistory = @"[{
    ""globalTradeID"": 25129732,
    ""tradeID"": ""6325758"",
    ""date"": ""2016-04-05 08:08:40"",
    ""rate"": ""0.02565498"",
    ""amount"": ""0.10000000"",
    ""total"": ""0.00256549"",
    ""fee"": ""0.00200000"",
    ""orderNumber"": ""34225313575"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 25129628,
    ""tradeID"": ""6325741"",
    ""date"": ""2016-04-05 08:07:55"",
    ""rate"": ""0.02565499"",
    ""amount"": ""0.10000000"",
    ""total"": ""0.00256549"",
    ""fee"": ""0.00200000"",
    ""orderNumber"": ""34225195693"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}]";

        #region CompletedOrderDetails

        private const string CompletedOrderDetails = @"[{
    ""globalTradeID"": 345739440,
    ""tradeID"": ""6238142"",
    ""date"": ""2018-02-16 19:55:15"",
    ""rate"": ""0.00005100"",
    ""amount"": ""7579.24108267"",
    ""total"": ""0.38654129"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""47874850339"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 345739439,
    ""tradeID"": ""6238141"",
    ""date"": ""2018-02-16 19:55:14"",
    ""rate"": ""0.00005100"",
    ""amount"": ""2.28017336"",
    ""total"": ""0.00011628"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""47874850339"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 345737315,
    ""tradeID"": ""6238069"",
    ""date"": ""2018-02-16 19:46:43"",
    ""rate"": ""0.00005115"",
    ""amount"": ""11486.30400782"",
    ""total"": ""0.58752444"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""47874023167"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 345736206,
    ""tradeID"": ""6238021"",
    ""date"": ""2018-02-16 19:39:23"",
    ""rate"": ""0.00005128"",
    ""amount"": ""9096.46996880"",
    ""total"": ""0.46646698"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""47873217973"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 345736149,
    ""tradeID"": ""6238020"",
    ""date"": ""2018-02-16 19:39:06"",
    ""rate"": ""0.00005128"",
    ""amount"": ""10000.00000000"",
    ""total"": ""0.51280000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""47873217973"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}]";

        #endregion

        private const string ReturnOrderTradesSell = @"[{
    ""globalTradeID"": 353843995,
    ""tradeID"": 1524326,
    ""currencyPair"": ""BTC_VIA"",
    ""type"": ""sell"",
    ""rate"": ""0.00017161"",
    ""amount"": ""119.83405116"",
    ""total"": ""0.02056472"",
    ""fee"": ""0.00250000"",
    ""date"": ""2018-03-15 02:00:31""
}, {
    ""globalTradeID"": 353843994,
    ""tradeID"": 1524325,
    ""currencyPair"": ""BTC_VIA"",
    ""type"": ""sell"",
    ""rate"": ""0.00017163"",
    ""amount"": ""11.65297442"",
    ""total"": ""0.00199999"",
    ""fee"": ""0.00250000"",
    ""date"": ""2018-03-15 02:00:31""
}, {
    ""globalTradeID"": 353843993,
    ""tradeID"": 1524324,
    ""currencyPair"": ""BTC_VIA"",
    ""type"": ""sell"",
    ""rate"": ""0.00017163"",
    ""amount"": ""11.65297442"",
    ""total"": ""0.00199999"",
    ""fee"": ""0.00250000"",
    ""date"": ""2018-03-15 02:00:31""
}]";

        private const string ReturnOrderTrades_SimpleBuy = @"[{
    ""globalTradeID"": 345736206,
    ""tradeID"": 6238021,
    ""currencyPair"": ""BTC_XEM"",
    ""type"": ""buy"",
    ""rate"": ""0.00005128"",
    ""amount"": ""9096.46996880"",
    ""total"": ""0.46646698"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-02-16 19:39:23""
}, {
    ""globalTradeID"": 345736149,
    ""tradeID"": 6238020,
    ""currencyPair"": ""BTC_XEM"",
    ""type"": ""buy"",
    ""rate"": ""0.00005128"",
    ""amount"": ""10000.00000000"",
    ""total"": ""0.51280000"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-02-16 19:39:06""
}]";

        #region ReturnOrderTrades_GasBuy

        private const string ReturnOrderTrades_GasBuy = @"[{
    ""globalTradeID"": 358351885,
    ""tradeID"": 130147,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.17821893"",
    ""total"": ""0.00707885"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:43:03""
}, {
    ""globalTradeID"": 358351682,
    ""tradeID"": 130146,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""4.39606430"",
    ""total"": ""0.17461167"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:41:03""
}, {
    ""globalTradeID"": 358351475,
    ""tradeID"": 130145,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""2.78493409"",
    ""total"": ""0.11061758"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:40:32""
}, {
    ""globalTradeID"": 358350781,
    ""tradeID"": 130144,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:34:58""
}, {
    ""globalTradeID"": 358350765,
    ""tradeID"": 130143,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:34:46""
}, {
    ""globalTradeID"": 358350760,
    ""tradeID"": 130142,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:34:41""
}, {
    ""globalTradeID"": 358350720,
    ""tradeID"": 130141,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.50949000"",
    ""total"": ""0.02023694"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:34:03""
}, {
    ""globalTradeID"": 358350626,
    ""tradeID"": 130140,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.43261515"",
    ""total"": ""0.01718347"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-04-01 02:33:23""
}, {
    ""globalTradeID"": 358329457,
    ""tradeID"": 130133,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.48059203"",
    ""total"": ""0.01908911"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-03-31 22:49:19""
}, {
    ""globalTradeID"": 358316481,
    ""tradeID"": 130128,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.00923445"",
    ""total"": ""0.00036679"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-03-31 21:05:15""
}, {
    ""globalTradeID"": 358284828,
    ""tradeID"": 130127,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.00730656"",
    ""total"": ""0.00029021"",
    ""fee"": ""0.00150000"",
    ""date"": ""2018-03-31 17:09:24""
}, {
    ""globalTradeID"": 358284727,
    ""tradeID"": 130126,
    ""currencyPair"": ""ETH_GAS"",
    ""type"": ""buy"",
    ""rate"": ""0.03971998"",
    ""amount"": ""8.27247449"",
    ""total"": ""0.32858252"",
    ""fee"": ""0.00250000"",
    ""date"": ""2018-03-31 17:08:09""
}]";

        #endregion

        #region GetCompletedOrderDetails_AllGas

        private const string ReturnOrderTrades_AllGas = @"[{
    ""globalTradeID"": 359099213,
    ""tradeID"": ""130931"",
    ""date"": ""2018-04-04 01:31:41"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.09270548"",
    ""total"": ""0.00382224"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359099165,
    ""tradeID"": ""130930"",
    ""date"": ""2018-04-04 01:31:33"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.49122807"",
    ""total"": ""0.02025333"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359099125,
    ""tradeID"": ""130929"",
    ""date"": ""2018-04-04 01:31:24"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098971,
    ""tradeID"": ""130928"",
    ""date"": ""2018-04-04 01:30:59"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098909,
    ""tradeID"": ""130927"",
    ""date"": ""2018-04-04 01:30:50"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098836,
    ""tradeID"": ""130926"",
    ""date"": ""2018-04-04 01:30:41"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098805,
    ""tradeID"": ""130925"",
    ""date"": ""2018-04-04 01:30:34"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098661,
    ""tradeID"": ""130924"",
    ""date"": ""2018-04-04 01:30:10"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098505,
    ""tradeID"": ""130923"",
    ""date"": ""2018-04-04 01:29:46"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098451,
    ""tradeID"": ""130922"",
    ""date"": ""2018-04-04 01:29:39"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.26835036"",
    ""total"": ""0.01106408"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359098353,
    ""tradeID"": ""130920"",
    ""date"": ""2018-04-04 01:29:11"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359097358,
    ""tradeID"": ""130919"",
    ""date"": ""2018-04-04 01:25:56"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.30075188"",
    ""total"": ""0.01240000"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359097294,
    ""tradeID"": ""130918"",
    ""date"": ""2018-04-04 01:25:44"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.49122807"",
    ""total"": ""0.02025333"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359097028,
    ""tradeID"": ""130917"",
    ""date"": ""2018-04-04 01:25:05"",
    ""rate"": ""0.04123000"",
    ""amount"": ""0.40853799"",
    ""total"": ""0.01684402"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359094184,
    ""tradeID"": ""130914"",
    ""date"": ""2018-04-04 01:11:45"",
    ""rate"": ""0.04123000"",
    ""amount"": ""1.61598982"",
    ""total"": ""0.06662726"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 359078956,
    ""tradeID"": ""130910"",
    ""date"": ""2018-04-04 00:02:42"",
    ""rate"": ""0.04123000"",
    ""amount"": ""3.63812757"",
    ""total"": ""0.14999999"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17565886729"",
    ""type"": ""sell"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358351885,
    ""tradeID"": ""130147"",
    ""date"": ""2018-04-01 02:43:03"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.17821893"",
    ""total"": ""0.00707885"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358351682,
    ""tradeID"": ""130146"",
    ""date"": ""2018-04-01 02:41:03"",
    ""rate"": ""0.03972000"",
    ""amount"": ""4.39606430"",
    ""total"": ""0.17461167"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358351475,
    ""tradeID"": ""130145"",
    ""date"": ""2018-04-01 02:40:32"",
    ""rate"": ""0.03972000"",
    ""amount"": ""2.78493409"",
    ""total"": ""0.11061758"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358350781,
    ""tradeID"": ""130144"",
    ""date"": ""2018-04-01 02:34:58"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358350765,
    ""tradeID"": ""130143"",
    ""date"": ""2018-04-01 02:34:46"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358350760,
    ""tradeID"": ""130142"",
    ""date"": ""2018-04-01 02:34:41"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.30969000"",
    ""total"": ""0.01230088"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358350720,
    ""tradeID"": ""130141"",
    ""date"": ""2018-04-01 02:34:03"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.50949000"",
    ""total"": ""0.02023694"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358350626,
    ""tradeID"": ""130140"",
    ""date"": ""2018-04-01 02:33:23"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.43261515"",
    ""total"": ""0.01718347"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358329457,
    ""tradeID"": ""130133"",
    ""date"": ""2018-03-31 22:49:19"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.48059203"",
    ""total"": ""0.01908911"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358316481,
    ""tradeID"": ""130128"",
    ""date"": ""2018-03-31 21:05:15"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.00923445"",
    ""total"": ""0.00036679"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358284828,
    ""tradeID"": ""130127"",
    ""date"": ""2018-03-31 17:09:24"",
    ""rate"": ""0.03972000"",
    ""amount"": ""0.00730656"",
    ""total"": ""0.00029021"",
    ""fee"": ""0.00150000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}, {
    ""globalTradeID"": 358284727,
    ""tradeID"": ""130126"",
    ""date"": ""2018-03-31 17:08:09"",
    ""rate"": ""0.03971998"",
    ""amount"": ""8.27247449"",
    ""total"": ""0.32858252"",
    ""fee"": ""0.00250000"",
    ""orderNumber"": ""17518581082"",
    ""type"": ""buy"",
    ""category"": ""exchange""
}]";

        #endregion

        #region GetCompletedOrderDetails_AllSymbols

        private const string GetCompletedOrderDetails_AllSymbolsOrders = @"{""BTC_MAID"": [ { ""globalTradeID"": 29251512, ""tradeID"": ""1385888"", ""date"": ""2016-05-03 01:29:55"", ""rate"": ""0.00014243"", ""amount"": ""353.74692925"", ""total"": ""0.05038417"", ""fee"": ""0.00200000"", ""orderNumber"": ""12603322113"", ""type"": ""buy"", ""category"": ""settlement"" }, { ""globalTradeID"": 29251511, ""tradeID"": ""1385887"", ""date"": ""2016-05-03 01:29:55"", ""rate"": ""0.00014111"", ""amount"": ""311.24262497"", ""total"": ""0.04391944"", ""fee"": ""0.00200000"", ""orderNumber"": ""12603319116"", ""type"": ""sell"", ""category"": ""marginTrade"" }";

        #endregion GetCompletedOrderDetails_AllSymbols

        private const string Unfilled = @"[{
    ""orderNumber"": ""35329211614"",
    ""type"": ""buy"",
    ""rate"": ""0.50000000"",
    ""startingAmount"": ""0.01000000"",
    ""amount"": ""0.01000000"",
    ""total"": ""0.00500000"",
    ""date"": ""2018-04-06 01:03:45"",
    ""margin"": 0
}]";

        private const string AllUnfilledOrders = @"{
    ""BTC_AMP"": [],
    ""BTC_ARDR"": [],
    ""BTC_BCH"": [],
    ""BTC_BCN"": [],
    ""BTC_BCY"": [],
    ""BTC_BELA"": [],
    ""BTC_BLK"": [],
    ""BTC_BTCD"": [],
    ""BTC_BTM"": [],
    ""BTC_BTS"": [],
    ""BTC_BURST"": [],
    ""BTC_CLAM"": [],
    ""BTC_CVC"": [],
    ""BTC_DASH"": [],
    ""BTC_DCR"": [],
    ""BTC_DGB"": [],
    ""BTC_DOGE"": [],
    ""BTC_EMC2"": [],
    ""BTC_ETC"": [],
    ""BTC_ETH"": [],
    ""BTC_EXP"": [],
    ""BTC_FCT"": [],
    ""BTC_FLDC"": [],
    ""BTC_FLO"": [],
    ""BTC_GAME"": [],
    ""BTC_GAS"": [],
    ""BTC_GNO"": [],
    ""BTC_GNT"": [],
    ""BTC_GRC"": [],
    ""BTC_HUC"": [],
    ""BTC_LBC"": [],
    ""BTC_LSK"": [],
    ""BTC_LTC"": [],
    ""BTC_MAID"": [],
    ""BTC_NAV"": [],
    ""BTC_NEOS"": [],
    ""BTC_NMC"": [],
    ""BTC_NXC"": [],
    ""BTC_NXT"": [],
    ""BTC_OMG"": [],
    ""BTC_OMNI"": [],
    ""BTC_PASC"": [],
    ""BTC_PINK"": [],
    ""BTC_POT"": [],
    ""BTC_PPC"": [],
    ""BTC_RADS"": [],
    ""BTC_REP"": [],
    ""BTC_RIC"": [],
    ""BTC_SBD"": [],
    ""BTC_SC"": [],
    ""BTC_STEEM"": [],
    ""BTC_STORJ"": [],
    ""BTC_STR"": [],
    ""BTC_STRAT"": [],
    ""BTC_SYS"": [],
    ""BTC_VIA"": [],
    ""BTC_VRC"": [],
    ""BTC_VTC"": [],
    ""BTC_XBC"": [],
    ""BTC_XCP"": [],
    ""BTC_XEM"": [],
    ""BTC_XMR"": [],
    ""BTC_XPM"": [],
    ""BTC_XRP"": [],
    ""BTC_XVC"": [],
    ""BTC_ZEC"": [],
    ""BTC_ZRX"": [],
    ""ETH_BCH"": [{
        ""orderNumber"": ""35329211614"",
        ""type"": ""buy"",
        ""rate"": ""0.50000000"",
        ""startingAmount"": ""0.01000000"",
        ""amount"": ""0.01000000"",
        ""total"": ""0.00500000"",
        ""date"": ""2018-04-06 01:03:45"",
        ""margin"": 0
    }],
    ""ETH_CVC"": [],
    ""ETH_ETC"": [],
    ""ETH_GAS"": [],
    ""ETH_GNO"": [],
    ""ETH_GNT"": [],
    ""ETH_LSK"": [],
    ""ETH_OMG"": [],
    ""ETH_REP"": [],
    ""ETH_STEEM"": [],
    ""ETH_ZEC"": [],
    ""ETH_ZRX"": [],
    ""USDT_BCH"": [],
    ""USDT_BTC"": [],
    ""USDT_DASH"": [],
    ""USDT_ETC"": [],
    ""USDT_ETH"": [],
    ""USDT_LTC"": [],
    ""USDT_NXT"": [],
    ""USDT_REP"": [],
    ""USDT_STR"": [],
    ""USDT_XMR"": [],
    ""USDT_XRP"": [],
    ""USDT_ZEC"": [],
    ""XMR_BCN"": [],
    ""XMR_BLK"": [],
    ""XMR_BTCD"": [],
    ""XMR_DASH"": [],
    ""XMR_LTC"": [],
    ""XMR_MAID"": [],
    ""XMR_NXT"": [],
    ""XMR_ZEC"": []
}";
#endregion
    }
}