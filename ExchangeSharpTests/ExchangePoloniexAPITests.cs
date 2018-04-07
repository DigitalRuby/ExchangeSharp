namespace ExchangeSharpTests
{
    using System;
    using System.Collections.Generic;

    using ExchangeSharp;
    using ExchangeSharp.API.Services;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using NSubstitute;

    [TestClass]
    public class ExchangePoloniexAPITests
    {
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

        #region AllMarketTradeHistory

        private const string AllMarketTradeHistory = @"{
    ""BTC_MAID"": [{
        ""globalTradeID"": 29251512,
        ""tradeID"": ""1385888"",
        ""date"": ""2016-05-03 01:29:55"",
        ""rate"": ""0.00014243"",
        ""amount"": ""353.74692925"",
        ""total"": ""0.05038417"",
        ""fee"": ""0.00200000"",
        ""orderNumber"": ""12603322113"",
        ""type"": ""buy"",
        ""category"": ""settlement""
    }, {
        ""globalTradeID"": 29251511,
        ""tradeID"": ""1385887"",
        ""date"": ""2016-05-03 01:29:55"",
        ""rate"": ""0.00014111"",
        ""amount"": ""311.24262497"",
        ""total"": ""0.04391944"",
        ""fee"": ""0.00200000"",
        ""orderNumber"": ""12603319116"",
        ""type"": ""sell"",
        ""category"": ""marginTrade""
    }, ...],
    ""BTC_LTC"": [...]...
}";

        #endregion

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
            var polo = new ExchangePoloniexAPI();
            ExchangeOrderResult order = polo.ParseOrder(singleOrder);
            order.OrderId.Should().Be("31226040");
            order.IsBuy.Should().BeTrue();
            order.Amount.Should().Be(338.8732m);
            order.OrderDate.Should().Be(new DateTime(2014, 10, 18, 23, 03, 21));
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
            var polo = new ExchangePoloniexAPI();
            ExchangeOrderResult order = polo.ParseOrder(singleOrder);
            order.OrderId.Should().Be("31226040");
            order.IsBuy.Should().BeFalse();
            order.Amount.Should().Be(338.8732m);
            order.OrderDate.Should().Be(new DateTime(2014, 10, 18, 23, 03, 21));
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
            ExchangePoloniexAPI.ParseOrderTrades(orderWithMultipleTrades, order);
            order.Amount.Should().Be(143.14m);
            order.AmountFilled.Should().Be(order.Amount);
            order.Fees.Should().Be(0.00006141m);
            order.FeesCurrency.Should().Be("BTC");
            order.IsBuy.Should().BeFalse();
            order.OrderId.Should().BeNullOrEmpty();
            order.AveragePrice.Should().Be(0.0001716132563851949140701411m);
            order.Price.Should().Be(order.AveragePrice);
            order.Symbol.Should().Be("BTC_VIA");
        }

        [TestMethod]
        public void ReturnOrderTrades_Buy_HasCorrectValues()
        {
            var order = new ExchangeOrderResult();
            var orderWithMultipleTrades = JsonConvert.DeserializeObject<JToken>(ReturnOrderTrades_SimpleBuy);
            ExchangePoloniexAPI.ParseOrderTrades(orderWithMultipleTrades, order);
            order.OrderId.Should().BeNullOrEmpty();
            order.Amount.Should().Be(19096.46996880m);
            order.AmountFilled.Should().Be(order.Amount);
            order.IsBuy.Should().BeTrue();
            order.Fees.Should().Be(28.64470495m);
            order.FeesCurrency.Should().Be("XEM");
            order.Symbol.Should().Be("BTC_XEM");
            order.Price.Should().Be(0.00005128m);
            order.AveragePrice.Should().Be(0.00005128m);
        }

        [TestMethod]
        public void ReturnOrderTrades_BuyComplicatedPriceAvg_IsCorrect()
        {
            var order = new ExchangeOrderResult();
            var orderWithMultipleTrades = JsonConvert.DeserializeObject<JToken>(ReturnOrderTrades_GasBuy);
            ExchangePoloniexAPI.ParseOrderTrades(orderWithMultipleTrades, order);
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
            var polo = new ExchangePoloniexAPI();
            JToken marketOrders = JsonConvert.DeserializeObject<JToken>(marketOrdersJson);

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            if (marketOrders is JArray array)
            {
                foreach (JToken token in array)
                {
                    orders.Add(polo.ParseOrder(token));
                }
            }

            orders[0].OrderId.Should().Be("120466");
            orders[0].IsBuy.Should().BeFalse();
            orders[0].Price.Should().Be(0.025m);
//            orders[0].Amount.Should().Be(100);

        }

        [TestMethod]
        public void GetOrderdetails_happyPath()
        {
            var requestHelper = Substitute.For<IRequestHelper>();
            requestHelper.MakeRequest(null).ReturnsForAnyArgs(ReturnOrderTrades_SimpleBuy);
            var polo = new ExchangePoloniexAPI(requestHelper);
            var response = polo.GetOrderDetails("1");
        }

        [TestMethod]
        public void ReturnOpenOrders_Unfilled_IsCorrect()
        {
            string unfilled = @"[{
    ""orderNumber"": ""35329211614"",
    ""type"": ""buy"",
    ""rate"": ""0.50000000"",
    ""startingAmount"": ""0.01000000"",
    ""amount"": ""0.01000000"",
    ""total"": ""0.00500000"",
    ""date"": ""2018-04-06 01:03:45"",
    ""margin"": 0
}]";

            var polo = new ExchangePoloniexAPI();
            JToken marketOrders = JsonConvert.DeserializeObject<JToken>(unfilled);
            ExchangeOrderResult order = polo.ParseOrder(marketOrders[0]);
            order.OrderId.Should().Be("35329211614");
            order.IsBuy.Should().BeTrue();
            order.AmountFilled.Should().Be(0);
            order.Amount.Should().Be(0.01m);
            order.OrderDate.Should().Be(new DateTime(2018, 4, 6, 1, 3, 45));
            order.Fees.Should().Be(0);
            order.FeesCurrency.Should().BeNullOrEmpty();
        }
    }
}