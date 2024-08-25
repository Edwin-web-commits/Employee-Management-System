

using BaseLibrary.DTOs;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ClientLibrary.Helpers
{
    public class GetHttpClient(IHttpClientFactory httpClientFactory, LocalStorageService localStorageService)
    {
        private const string HeaderKey = "Authorization";
        public async Task<HttpClient> GetPrivateHttpClient()
        {
            var client = httpClientFactory.CreateClient("SystemApiClient");
            var stringToken = await localStorageService.GetToken();
            if(string.IsNullOrEmpty(stringToken)) 
            { 
                return client;
            }
            var deserializeToken = Serializations.DeserializeJsonString<UserSession>(stringToken);
            if(deserializeToken == null)
            {
                return client;
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", deserializeToken.Token);
            return client;
        }

        //For Http request that do not need authorization. e.g Login, Register, RefreshToken.
        public HttpClient GetPublicHttpClient()
        {
            var client = httpClientFactory.CreateClient("SystemApiClient");
            client.DefaultRequestHeaders.Remove(HeaderKey); //Remove the Authorization headers
            return client;
        }
    }
}
