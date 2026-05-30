using System.Net.Http.Json;
using System.Text.Json;

namespace ShoppingListServer.Tests.Support;

public static class HttpClientJsonExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync(this HttpClient client, string requestUri, object value)
    {
        return client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(value)
        });
    }

    public static Task<HttpResponseMessage> DeleteAsJsonAsync(this HttpClient client, string requestUri, object value)
    {
        return client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, requestUri)
        {
            Content = JsonContent.Create(value)
        });
    }

    public static Task<HttpResponseMessage> PostEmptyAsync(this HttpClient client, string requestUri)
    {
        return client.PostAsync(requestUri, JsonContent.Create(string.Empty));
    }

    public static async Task<JsonDocument> ReadJsonDocumentAsync(this HttpContent content)
    {
        await using var stream = await content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
