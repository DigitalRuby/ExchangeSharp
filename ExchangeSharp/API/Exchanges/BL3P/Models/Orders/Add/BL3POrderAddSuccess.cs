using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models.Orders.Add
{
	internal class BL3POrderAddSuccess : BL3PResponsePayload
	{
		[JsonProperty("order_id", Required = Required.Always)]
		public string OrderId { get; set; }
	}
}
