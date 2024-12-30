using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.API.Exchanges.MEXC.Models
{
	internal class MarketDepthDiffUpdateDetailItem
	{
		[JsonProperty("p")]
		public decimal Price { get; set; }

		[JsonProperty("v")]
		public decimal Volume { get; set; }
	}

	internal class MarketDepthDiffUpdateDetails
	{
		public List<MarketDepthDiffUpdateDetailItem> Asks { get; set; }

		public List<MarketDepthDiffUpdateDetailItem> Bids { get; set; }

		[JsonProperty("e")]
		public string EventType { get; set; }

		[JsonProperty("r")]
		public long Version { get; set; }
	}

	internal class MarketDepthDiffUpdate
	{
		[JsonProperty("c")]
		public string Channel { get; set; }

		[JsonProperty("d")]
		public MarketDepthDiffUpdateDetails Details { get; set; }

		[JsonProperty("s")]
		public string Symbol { get; set; }

		/// <summary>
		/// Milliseconds since Unix epoch
		/// </summary>
		[JsonProperty("t")]
		public long EventTime { get; set; }
	}
}
