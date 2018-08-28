using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;

namespace ExchangeSharpTests
{
    public class MockAPIRequestMaker : IAPIRequestMaker
    {
        /// <summary>
        /// Use a global response for all requests
        /// </summary>
        public string GlobalResponse { get; set; }

        /// <summary>
        /// Otherwise lookup the url in the dictionary and find the response, which is string result or Exception to throw
        /// </summary>
        public Dictionary<string, object> UrlAndResponse = new Dictionary<string, object>();

        /// <summary>
        /// Request state changed
        /// </summary>
        public Action<IAPIRequestMaker, RequestMakerState, object> RequestStateChanged { get; set; }

        /// <summary>
        /// Make a mock request
        /// </summary>
        /// <param name="url"></param>
        /// <param name="baseUrl"></param>
        /// <param name="payload"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public async Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null)
        {
            await new SynchronizationContextRemover();
            RequestStateChanged?.Invoke(this, RequestMakerState.Begin, null);
            if (GlobalResponse != null)
            {
                RequestStateChanged?.Invoke(this, RequestMakerState.Finished, GlobalResponse);
                return GlobalResponse;
            }
            else if (UrlAndResponse.TryGetValue(url, out object response))
            {
                if (!(response is Exception ex))
                {
                    RequestStateChanged?.Invoke(this, RequestMakerState.Finished, response as string);
                    return response as string;
                }
                RequestStateChanged?.Invoke(this, RequestMakerState.Error, ex);
                throw ex;
            }
            return @"{ ""error"": ""No result from server"" }";
        }
    }
}
