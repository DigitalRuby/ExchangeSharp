using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute.ExceptionExtensions;
using static System.Net.WebRequestMethods;

namespace ExchangeSharpTests
{
	[TestClass]
	public sealed class ExchangeCoinbaseAPITests
	{
		private async Task<ExchangeCoinbaseAPI> CreateSandboxApiAsync(string response = null)
		{ // per Coinbase: All responses are static and pre-defined. https://docs.cdp.coinbase.com/advanced-trade/docs/rest-api-sandbox. Thus,  no need to use MockAPIRequestMaker and just connect to real sandbox every time
			//var requestMaker = new MockAPIRequestMaker();
			//if (response != null)
			//{
			//	requestMaker.GlobalResponse = response;
			//}
			var api = (
					await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.Coinbase) as ExchangeCoinbaseAPI
			)!;
			//api.RequestMaker = requestMaker;
			api.BaseUrl = "https://api-sandbox.coinbase.com/api/v3/brokerage";
			return api;
		}

		[TestMethod]
		public async Task PlaceOrderAsync_Success()
		{
			var api = await CreateSandboxApiAsync();
			{ // per Coinbase: Users can make API requests to Advanced sandbox API without authentication. https://docs.cdp.coinbase.com/advanced-trade/docs/rest-api-sandbox. Thus, no need to set api.PublicApiKey and api.PrivateApiKey
				var orderRequest = new ExchangeOrderRequest
				{
					MarketSymbol = "BTC-USD",
					Amount = 0.0001m,
					Price = 10000m,
					IsBuy = true,
					OrderType = OrderType.Limit
				};

				var orderResult = await api.PlaceOrderAsync(orderRequest);
				orderResult.Should().NotBeNull("an order result should be returned");
				orderResult.ClientOrderId.Should().Be("sandbox_success_order", "this is what the sandbox returns");
				orderResult.OrderId.Should().Be("f898eaf4-6ffc-47be-a159-7ff292e5cdcf", "this is what the sandbox returns");
				orderResult.MarketSymbol.Should().Be(orderRequest.MarketSymbol, "the market symbol should be the same as requested");
				orderResult.IsBuy.Should().Be(false, "the sandbox always returns a SELL, even if a buy is submitted");
				orderResult.Result.Should().Be(ExchangeAPIOrderResult.PendingOpen, "the order should be placed successfully in sandbox");
			}
		}

		[TestMethod]
		public async Task PlaceOrderAsync_Failure()
		{
			var api = await CreateSandboxApiAsync();
			{ // per Coinbase: Users can make API requests to Advanced sandbox API without authentication. https://docs.cdp.coinbase.com/advanced-trade/docs/rest-api-sandbox. Thus, no need to set api.PublicApiKey and api.PrivateApiKey
				var orderRequest = new ExchangeOrderRequest
				{
					MarketSymbol = "BTC-USD",
					Amount = 0.0001m,
					Price = 10000m,
					IsBuy = true,
					OrderType = OrderType.Limit
				};
				APIRequestMaker.AdditionalHeader = ("X-Sandbox", "PostOrder_insufficient_fund");
				var orderResult = await api.PlaceOrderAsync(orderRequest);
				orderResult.Should().NotBeNull("an order result should be returned");
				orderResult.ClientOrderId.Should().Be("9dde8003-fc68-4ec1-96ab-5a759e5f4f5c", "this is what the sandbox returns");
				orderResult.OrderId.Should().BeNull("this is what the sandbox returns");
				orderResult.MarketSymbol.Should().Be(orderRequest.MarketSymbol, "because the market symbol should be the same as requested");
				orderResult.IsBuy.Should().Be(orderRequest.IsBuy, "this is what was requested");
				orderResult.Result.Should().Be(ExchangeAPIOrderResult.Rejected, "testing for failure");
			}
		}

		[TestMethod]
		public async Task CancelOrderAsync_Success()
		{
			var api = await CreateSandboxApiAsync();
			await api.CancelOrderAsync("1");
		}

		[TestMethod]
		public async Task CancelOrderAsync_Failure()
		{
			var api = await CreateSandboxApiAsync();
			APIRequestMaker.AdditionalHeader = ("X-Sandbox", "CancelOrders_failure");
			Func<Task> act = () => api.CancelOrderAsync("1");
			await act.Should().ThrowAsync<APIException>();
		}
	}
}
