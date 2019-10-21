using Newtonsoft.Json;

namespace ExchangeSharp.NDAX
{
    public class SendOrderResponse
    {
        [JsonProperty("errormsg")]
        public string ErrorMsg { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("OrderId")]
        public int OrderId { get; set; }
    }
}