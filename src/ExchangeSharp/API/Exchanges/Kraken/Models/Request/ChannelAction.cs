using System;
using System.Collections.Generic;
using System.Text;
using ExchangeSharp.API.Exchanges.Kraken.Models.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ExchangeSharp.API.Exchanges.Kraken.Models.Request
{
	internal class ChannelAction
	{
		[JsonConverter(typeof(StringEnumConverter))]
		[JsonProperty("event")]
		public ActionType Event { get; set; }

		[JsonProperty("pair")]
		public List<string> Pairs { get; set; }

		[JsonProperty("subscription")]
		public Subscription SubscriptionSettings { get; set; }
	}
}
