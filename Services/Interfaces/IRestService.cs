using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IRestService
    {
        HttpClient GetClient();
        Task<HttpResponseMessage> CreatePostRequest(string uriString, string jsonBody);
    }
}
