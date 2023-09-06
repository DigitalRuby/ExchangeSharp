using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.Kraken.Models.Types
{
	internal class Subscription
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("depth")]
		public int Depth { get; set; }
	}
}
