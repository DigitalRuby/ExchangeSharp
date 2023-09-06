using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3POrderAddSuccess : BL3PResponsePayload
	{
		[JsonProperty("order_id", Required = Required.Always)]
		public string OrderId { get; set; }
	}
}
