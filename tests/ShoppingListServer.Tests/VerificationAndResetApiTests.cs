using System.Net;
using System.Net.Http.Json;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests;

public sealed class VerificationAndResetApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public VerificationAndResetApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VerificationEndpoints_WorkWithFakeMailService()
    {
        var user = await _factory.CreateUserAsync(UniqueEmail(), "Password123!", isVerified: false);
        using var userClient = _factory.CreateAuthenticatedClient(user);
        using var anonymousClient = _factory.CreateClient();

        var resendResponse = await userClient.PostEmptyAsync("/verify/resendVerificationEMail");
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        var code = _factory.EmailVerificationService.GetCodeForUser(user.Id);
        var verifyResponse = await anonymousClient.GetAsync($"/verify/em/{code}");
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var html = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Successfully registered", html);
    }

    [Fact]
    public async Task ResetPasswordEndpoints_WorkEndToEnd()
    {
        var user = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var anonymousClient = _factory.CreateClient();

        var requestCodeResponse = await anonymousClient.PostAsJsonAsync("/rp/requestcode", new { Item1 = user.Email });
        Assert.Equal(HttpStatusCode.OK, requestCodeResponse.StatusCode);

        var code = _factory.ResetPasswordService.GetCodeForEmail(user.Email);

        var codeValidResponse = await anonymousClient.PostAsJsonAsync("/rp/codevalid", new
        {
            Item1 = user.Email,
            Item2 = code
        });
        Assert.Equal(HttpStatusCode.OK, codeValidResponse.StatusCode);

        var resetPasswordResponse = await anonymousClient.PostAsJsonAsync("/rp/resetpassword", new
        {
            Item1 = user.Email,
            Item2 = code,
            Item3 = "BrandNewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, resetPasswordResponse.StatusCode);

        var authenticateResponse = await anonymousClient.PostAsJsonAsync("/users/authenticate", new
        {
            Email = user.Email,
            Password = "BrandNewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, authenticateResponse.StatusCode);
    }

    private static string UniqueEmail()
    {
        return $"verify-{Guid.NewGuid():N}@example.test";
    }
}
