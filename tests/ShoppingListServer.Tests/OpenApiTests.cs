using System.Text.Json;
using System.Text.RegularExpressions;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests;

public sealed class OpenApiTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedRoutes = new Dictionary<string, string[]>
    {
        ["/users/test"] = new[] { "head" },
        ["/users/authenticate"] = new[] { "post" },
        ["/users/register"] = new[] { "post" },
        ["/users/register_apple"] = new[] { "post" },
        ["/users/register_google"] = new[] { "post" },
        ["/users/register_facebook"] = new[] { "post" },
        ["/users/user"] = new[] { "patch", "delete" },
        ["/users/password"] = new[] { "patch" },
        ["/users/rp/{}"] = new[] { "get" },
        ["/users/{}"] = new[] { "get" },
        ["/users/contactlink/{}"] = new[] { "post" },
        ["/users/contact"] = new[] { "post", "patch" },
        ["/users/contact/{}"] = new[] { "delete" },
        ["/users/contacts"] = new[] { "get" },
        ["/users/generate_share_id"] = new[] { "post" },
        ["/users/contact_share_id"] = new[] { "post" },
        ["/users/contact_share_id/{}"] = new[] { "get" },
        ["/users/csid/{}"] = new[] { "get" },
        ["/users/register_fcm_token"] = new[] { "post" },
        ["/users/unregister_fcm_token"] = new[] { "post" },
        ["/users/profile_picture"] = new[] { "post", "delete" },
        ["/users/profile_picture_transformation"] = new[] { "post" },
        ["/users/profile_picture_lastchange"] = new[] { "get" },
        ["/users/profile_picture_info/{}"] = new[] { "get" },
        ["/users/profile_picture/{}"] = new[] { "get" },
        ["/shopping/list/{}"] = new[] { "get", "delete" },
        ["/shopping/list_lastchange/{}"] = new[] { "get" },
        ["/shopping/lists"] = new[] { "get" },
        ["/shopping/lists_lastchange"] = new[] { "get" },
        ["/shopping/list"] = new[] { "post", "patch" },
        ["/shopping/item"] = new[] { "patch", "delete" },
        ["/shopping/listproperty"] = new[] { "patch" },
        ["/shopping/product"] = new[] { "patch" },
        ["/shopping/listpermission/{}"] = new[] { "get" },
        ["/shopping/listpermission"] = new[] { "get", "put" },
        ["/shopping/listpermission/{}/{}"] = new[] { "get", "delete" },
        ["/shopping/generate_share_id/{}"] = new[] { "post" },
        ["/shopping/list_share_id"] = new[] { "post" },
        ["/shopping/list_share_id/{}"] = new[] { "get" },
        ["/shopping/lsid/{}"] = new[] { "get" },
        ["/shopping/test_firebase_push_message"] = new[] { "post" },
        ["/verify/resendverificationemail"] = new[] { "post" },
        ["/verify/em/{}"] = new[] { "get" },
        ["/rp/requestcode"] = new[] { "post" },
        ["/rp/codevalid"] = new[] { "post" },
        ["/rp/resetpassword"] = new[] { "post" }
    };

    private readonly HttpClient _client;

    public OpenApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerDocument_PublishesBearerSecurityAndAllKnownRoutes()
    {
        var swaggerUiResponse = await _client.GetAsync("/swagger/index.html");
        Assert.True(swaggerUiResponse.IsSuccessStatusCode);

        var swaggerJsonResponse = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.True(swaggerJsonResponse.IsSuccessStatusCode);

        using var document = await swaggerJsonResponse.Content.ReadJsonDocumentAsync();
        var root = document.RootElement;
        Assert.Equal("3.0.1", root.GetProperty("openapi").GetString());

        var securitySchemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());

        var routes = root.GetProperty("paths")
            .EnumerateObject()
            .ToDictionary(
                entry => NormalizePath(entry.Name),
                entry => entry.Value.EnumerateObject().Select(method => method.Name.ToLowerInvariant()).ToHashSet());

        foreach (var expectedRoute in ExpectedRoutes)
        {
            Assert.True(routes.TryGetValue(expectedRoute.Key, out var methods), $"Missing route {expectedRoute.Key} in Swagger document.");
            foreach (var expectedMethod in expectedRoute.Value)
            {
                Assert.Contains(expectedMethod, methods);
            }
        }
    }

    private static string NormalizePath(string path)
    {
        var lower = path.ToLowerInvariant();
        return Regex.Replace(lower, "\\{[^}]+\\}", "{}");
    }
}
