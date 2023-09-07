using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp.BinanceGroup;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeApolloXApi : BinanceGroupCommon
	{
		public override string BaseUrl { get; set; } = "https://fapi.apollox.finance";
		public override string BaseUrlWebSocket { get; set; } = "wss://fstream.apollox.finance";

		public override string BaseUrlApi => $"{BaseUrl}/fapi/v1";
	}

	public partial class ExchangeName
	{
		public const string ApolloX = "ApolloX";
	}
}
