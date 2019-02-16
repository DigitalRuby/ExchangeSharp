using System;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    [TestClass]
    public sealed class ExchangeBitBankTests
    {
        private ExchangeBitBankAPI makeMockRequestMaker(string response = null)
        {
            var requestMaker = new MockAPIRequestMaker();
            if (response != null)
            {
                requestMaker.GlobalResponse = response;
            }
            return new ExchangeBitBankAPI() { RequestMaker = requestMaker };
        }

        # region Public API

        [TestMethod]
        public async Task ShouldParseGetTickerResult()
        {
            var data = @"
            { success: 1,
                data: {
                    sell: '395733',
                    buy: '395648',
                    high: '397562',
                    low: '393668',
                    last: '395556',
                    vol: '719.0633',
                    timestamp: 1550087192693
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            var ticker = await api.GetTickerAsync("BTC-JPY");
            ticker.MarketSymbol.Should().Be("btc_jpy");
            ticker.Ask.Should().Be(395733m);
            ticker.Bid.Should().Be(395648m);
            ticker.Last.Should().Be(395556m);
            ticker.Volume.BaseCurrencyVolume.Should().Be(719.0633m);
        }

        [TestMethod]
        public async Task ShouldGetTransactions()
        {
            var data = @"
            {
                success: 1,
                data: {
                    transactions:[
                        {
                            transaction_id: 29039731,
                            side: 'sell',
                            price: '395939',
                            amount: '0.0382',
                            executed_at: 1550112110441
                        },
                        {
                            transaction_id: 29039683,
                            side: 'buy',
                            price: '396801',
                            amount: '0.0080',
                            executed_at: 1550111567665
                        }
                    ]
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            ExchangeOrderBook resp = await api.GetOrderBookAsync("BTC-JPY");
            resp.MarketSymbol.Should().Be("btc_jpy");
            resp.Asks.Should().HaveCount(1);
            resp.Bids.Should().HaveCount(1);
        }

        [TestMethod]
        public async Task ShouldGetCandleStick()
        {
            var data = @"
            {
                success: 1,
                data: {
                    candlestick: [
                        {
                            type: '1hour',
                            ohlcv: [
                                [
                                    '1662145',
                                    '1665719',
                                    '1612861',
                                    '1629941',
                                    '5.8362',
                                    1514160000000
                                ],
                                [
                                    '0.01173498',
                                    '0.01173498',
                                    '0.01160568',
                                    '0.01160571',
                                    '95.0761',
                                    1549674000000
                                ]
                            ]
                        }
                    ],
                    timestamp: 1514246399496
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            // starttime is required.
            // await Assert.ThrowsExceptionAsync<APIException>(async () => await api.GetCandlesAsync("BTC-JPY", 3600));
            var resp = await api.GetCandlesAsync("BTC-JPY", 3600, DateTime.UtcNow);
            MarketCandle candle = resp.First();
            candle.ExchangeName.Should().Be("BitBank");
            candle.OpenPrice.Should().Be(1662145m);
            candle.HighPrice.Should().Be(1665719m);
            candle.LowPrice.Should().Be(1612861m);
            candle.ClosePrice.Should().Be(1629941);
            candle.BaseCurrencyVolume.Should().Be(5.8362);
            candle.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1514160000000).DateTime);
        }

        # endregion

        # region Private API
        [TestMethod]
        public async Task ShouldGetAssets()
        {
            var data = @"
            {
                success: 1,
                data: {
                    assets: [
                        {
                            asset: 'jpy',
                            amount_precision: 4,
                            onhand_amount: '3551.9501',
                            locked_amount: '0.0000',
                            free_amount: '3551.9501',
                            stop_deposit: false,
                            stop_withdrawal: false,
                            withdrawal_fee: {
                                threshold: '30000.0000',
                                under: '540.0000',
                                over: '756.0000'
                            }
                        }
                    ]
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            var resp = await api.GetAmountsAsync();
            resp.First().Key.Should().Equals("JPY");
            resp.First().Value.Should().Equals(3551.9501m);
        }

        [TestMethod]
        public async Task ShouldGetOrderDetail()
        {
            var data = @"
            {
                success: 1,
                data: {
                    order_id: 558167000,
                    pair: 'btc_jpy',
                    side: 'sell',
                    type: 'limit',
                    start_amount: '0.00400000',
                    remaining_amount: '0.00000000',
                    executed_amount: '0.00400000',
                    price: '395254.0000',
                    average_price: '395254.0000',
                    ordered_at: 1550096080188,
                    executed_at: 1550096081545,
                    status: 'FULLY_FILLED'
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            // Throws error when no Market Symbol
            var resp = await api.GetOrderDetailsAsync("558167000", "BTC-JPY");
            ExchangeOrderResult resp2 = await api.GetOrderDetailsAsync("58037954");
            resp.Should().BeEquivalentTo(resp2);
        }


        [TestMethod]
        public async Task ShouldGetOrders()
        {
            var data = @"
            {
                success: 1,
                data: {
                    orders:[
                        {
                            order_id: 558167037,
                            pair: 'btc_jpy',
                            side: 'sell',
                            type: 'limit',
                            start_amount: '0.00400000',
                            remaining_amount: '0.00000000',
                            executed_amount: '0.00400000',
                            price: '395254.0000',
                            average_price: '395254.0000',
                            ordered_at: 1550096080188,
                            executed_at: 1550096081545,
                            status: 'FULLY_FILLED'
                        }
                    ]
                }
            }
            ";
            var api = makeMockRequestMaker(data);
            var orderBooks = await api.GetOpenOrderDetailsAsync();
            ExchangeOrderResult order = orderBooks.First();
            order.IsBuy.Should().BeFalse();
        }

        # endregion
    }
}