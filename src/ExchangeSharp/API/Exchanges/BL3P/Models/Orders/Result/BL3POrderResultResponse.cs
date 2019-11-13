using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3POrderResultResponse : BL3PResponse<BL3POrderResultSuccess>
	{
		[JsonConverter(typeof(BL3PResponseConverter<BL3POrderResultSuccess>))]
		protected override BL3PResponsePayload Data { get; set; }
	}
}
