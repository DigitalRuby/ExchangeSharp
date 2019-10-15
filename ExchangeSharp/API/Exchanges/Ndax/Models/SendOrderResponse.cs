using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.Ndax.Models
{
    public class SendOrderResponse
    {
        [JsonProperty("errormsg")]
        public bool ErrorMsg { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("OrderId")]
        public int OrderId { get; set; }
    }
}