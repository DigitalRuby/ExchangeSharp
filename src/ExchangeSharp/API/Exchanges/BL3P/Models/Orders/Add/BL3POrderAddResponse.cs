using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3POrderAddResponse : BL3PResponse<BL3POrderAddSuccess>
	{
		[JsonConverter(typeof(BL3PResponseConverter<BL3POrderAddSuccess>))]
		protected override BL3PResponsePayload Data { get; set; }
	}
}
