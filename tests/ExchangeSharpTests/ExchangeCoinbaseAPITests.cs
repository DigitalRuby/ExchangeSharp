using ExchangeSharp;
using ExchangeSharp.Coinbase;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharpTests
{
	[TestClass]
	public sealed class ExchangeCoinbaseAPITests
	{
		private async Task<ExchangeCoinbaseAPI> MakeMockRequestMaker(string response = null)
		{
			var requestMaker = new MockAPIRequestMaker();
			if (response != null)
			{
				requestMaker.GlobalResponse = response;
			}
			var api = (await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.Coinbase) as ExchangeCoinbaseAPI)!;
			api.RequestMaker = requestMaker;
			return api;
		}

		[TestMethod]
		public void DeserializeUserMatch()
		{
			string toParse = "{\r\n  \"type\": \"match\",\r\n  \"trade_id\": 10,\r\n  \"sequence\": 50,\r\n  \"maker_order_id\": \"ac928c66-ca53-498f-9c13-a110027a60e8\",\r\n  \"taker_order_id\": \"132fb6ae-456b-4654-b4e0-d681ac05cea1\",\r\n  \"time\": \"2014-11-07T08:19:27.028459Z\",\r\n  \"product_id\": \"BTC-USD\",\r\n  \"size\": \"5.23512\",\r\n  \"price\": \"400.23\",\r\n  \"side\": \"sell\"\r\n,\r\n  \"taker_user_id\": \"5844eceecf7e803e259d0365\",\r\n  \"user_id\": \"5844eceecf7e803e259d0365\",\r\n  \"taker_profile_id\": \"765d1549-9660-4be2-97d4-fa2d65fa3352\",\r\n  \"profile_id\": \"765d1549-9660-4be2-97d4-fa2d65fa3352\",\r\n  \"taker_fee_rate\": \"0.005\"\r\n}";

			var usermatch = JsonConvert.DeserializeObject<Match>(toParse, BaseAPI.SerializerSettings);
			usermatch.MakerOrderId.Should().Be("ac928c66-ca53-498f-9c13-a110027a60e8");
			usermatch.ExchangeOrderResult.OrderId.Should().Be("132fb6ae-456b-4654-b4e0-d681ac05cea1");
		}
	}
}
