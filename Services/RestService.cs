using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class RestService : IRestService
    {
        private HttpClient Client { get; set; }

        public RestService()
        {
            Client = new HttpClient();
        }

        public HttpClient GetClient()
        {
            return Client;
        }

        public async Task<HttpResponseMessage> CreatePostRequest(string uriString, string jsonBody)
        {
            try
            {
                Uri uri = new Uri(uriString);
                StringContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await Client.PostAsync(uri, content);
                if (!response.IsSuccessStatusCode)
                {
                    response.ReasonPhrase = await response.Content.ReadAsStringAsync();
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreatePostRequest {0}", ex.Message);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }
    }
}
