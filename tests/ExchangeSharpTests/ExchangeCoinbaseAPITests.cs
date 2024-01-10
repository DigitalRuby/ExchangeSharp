using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

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
			var api = (
					await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.Coinbase) as ExchangeCoinbaseAPI
			)!;
			api.RequestMaker = requestMaker;
			return api;
		}
	}
}
