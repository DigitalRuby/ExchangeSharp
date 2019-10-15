using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.Ndax.Models
{
    public partial class DepositInfo
    {
        [JsonProperty("AssetManagerId")]
        public long AssetManagerId { get; set; }

        [JsonProperty("AccountId")]
        public long AccountId { get; set; }

        [JsonProperty("AssetId")]
        public long AssetId { get; set; }

        [JsonProperty("ProviderId")]
        public long ProviderId { get; set; }

        [JsonProperty("DepositInfo")]
        public string DepositInfoDepositInfo { get; set; }

        [JsonProperty("result")]
        public bool Result { get; set; }

        [JsonProperty("errormsg")]
        public object Errormsg { get; set; }

        [JsonProperty("statuscode")]
        public long Statuscode { get; set; }

        public ExchangeDepositDetails ToExchangeDepositDetails(string cryptoCode)
        {
            if (!Result)
            {
                throw new APIException($"{Errormsg}");
            }
            return new ExchangeDepositDetails()
            {
                Address = null,
                Currency = cryptoCode
            };
            
        }
    }
}