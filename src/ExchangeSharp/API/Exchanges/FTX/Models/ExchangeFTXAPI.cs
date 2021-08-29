using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp.API.Exchanges.FTX.Models
{
	public sealed partial class ExchangeFTXAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://ftx.com/api";

		protected async override Task<IEnumerable<string>> OnGetMarketSymbolsAsync(bool isWebSocket = false)
		{
			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			var names = result.Children().Select(x => x["name"].ToString()).ToList();

			names.Sort();

			return names;
		}
	}
}
