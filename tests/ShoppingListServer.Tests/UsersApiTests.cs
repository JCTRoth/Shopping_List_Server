using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests;

public sealed class UsersApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UsersApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HeadAuthenticateAndRegister_ReturnExpectedDtos()
    {
        using var anonymousClient = _factory.CreateClient();

        var headResponse = await anonymousClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/users/test"));
        Assert.Equal(HttpStatusCode.OK, headResponse.StatusCode);

        var registerEmail = UniqueEmail();
        var registerResponse = await anonymousClient.PostAsJsonAsync("/users/register", new
        {
            EMail = registerEmail,
            FirstName = "Local",
            LastName = "User",
            Username = "local-user",
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using (var registerDocument = await registerResponse.Content.ReadJsonDocumentAsync())
        {
            AssertSafeUserPayload(registerDocument.RootElement, expectToken: false);
        }

        var user = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var authenticateResponse = await anonymousClient.PostAsJsonAsync("/users/authenticate", new
        {
            Email = user.Email,
            Password = user.Password
        });
        Assert.Equal(HttpStatusCode.OK, authenticateResponse.StatusCode);

        using var authenticateDocument = await authenticateResponse.Content.ReadJsonDocumentAsync();
        AssertSafeUserPayload(authenticateDocument.RootElement, expectToken: true);
    }

    [Theory]
    [InlineData("/users/register_apple")]
    [InlineData("/users/register_google")]
    [InlineData("/users/register_facebook")]
    public async Task SocialRegistrationEndpoints_ReturnSafeUserDtos(string route)
    {
        using var client = _factory.CreateClient();
        var payload = CreateSocialRegistrationPayload(route, UniqueEmail());

        var response = await client.PostAsJsonAsync(route, payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await response.Content.ReadJsonDocumentAsync();
        AssertSafeUserPayload(document.RootElement, expectToken: false);
    }

    [Fact]
    public async Task ContactAndShareEndpoints_ReturnSafeDtos()
    {
        var source = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var target = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var sourceClient = _factory.CreateAuthenticatedClient(source);
        using var targetClient = _factory.CreateAuthenticatedClient(target);

        var addContactResponse = await sourceClient.PostAsJsonAsync("/users/contact", new
        {
            User = new { Id = target.Id },
            Type = UserContactType.Default
        });
        Assert.Equal(HttpStatusCode.OK, addContactResponse.StatusCode);

        var contactsResponse = await sourceClient.GetAsync("/users/contacts");
        Assert.Equal(HttpStatusCode.OK, contactsResponse.StatusCode);
        using (var contactsDocument = await contactsResponse.Content.ReadJsonDocumentAsync())
        {
            var contact = Assert.Single(contactsDocument.RootElement.EnumerateArray());
            var properties = GetProperties(contact.GetProperty("user"));
            Assert.Equal(target.Id, properties["id"].GetString());
            Assert.False(properties.ContainsKey("token"));
            Assert.False(properties.ContainsKey("role"));
            Assert.False(properties.ContainsKey("externalid"));
        }

        var removeContactResponse = await sourceClient.DeleteAsync($"/users/contact/{target.Id}");
        Assert.Equal(HttpStatusCode.OK, removeContactResponse.StatusCode);

        var shareIdResponse = await targetClient.PostEmptyAsync("/users/generate_share_id");
        Assert.Equal(HttpStatusCode.OK, shareIdResponse.StatusCode);
        var shareId = await shareIdResponse.Content.ReadFromJsonAsync<string>();
        Assert.False(string.IsNullOrWhiteSpace(shareId));

        var addByShareIdResponse = await sourceClient.PostAsJsonAsync("/users/contact_share_id", new { Item1 = shareId });
        Assert.Equal(HttpStatusCode.OK, addByShareIdResponse.StatusCode);
        using (var shareDocument = await addByShareIdResponse.Content.ReadJsonDocumentAsync())
        {
            AssertSafeUserPayload(shareDocument.RootElement, expectToken: false);
        }

        var lookupByShareIdResponse = await sourceClient.GetAsync($"/users/contact_share_id/{shareId}");
        Assert.Equal(HttpStatusCode.OK, lookupByShareIdResponse.StatusCode);
        using (var lookupDocument = await lookupByShareIdResponse.Content.ReadJsonDocumentAsync())
        {
            AssertSafeUserPayload(lookupDocument.RootElement, expectToken: false);
        }
    }

    [Fact]
    public async Task CurrentUserMutationEndpoints_WorkAndDeleteUser()
    {
        var user = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var admin = await _factory.CreateUserAsync(UniqueEmail(), "Password123!", role: Role.Admin);
        using var userClient = _factory.CreateAuthenticatedClient(user);
        using var anonymousClient = _factory.CreateClient();
        using var adminClient = _factory.CreateAuthenticatedClient(admin);

        var updateResponse = await userClient.PatchAsJsonAsync("/users/user", new
        {
            EMail = user.Email,
            FirstName = "Updated",
            LastName = "User",
            Username = "updated-user",
            ColorArgb = 42
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getUpdatedResponse = await adminClient.GetAsync($"/users/{user.Id}");
        Assert.Equal(HttpStatusCode.OK, getUpdatedResponse.StatusCode);
        using (var updatedDocument = await getUpdatedResponse.Content.ReadJsonDocumentAsync())
        {
            var properties = GetProperties(updatedDocument.RootElement);
            Assert.Equal("Updated", properties["firstname"].GetString());
            Assert.Equal(42, properties["colorargb"].GetInt32());
            Assert.False(properties.ContainsKey("token"));
        }

        var registerFcmResponse = await userClient.PostAsJsonAsync("/users/register_fcm_token", new { Item1 = "fcm-token-1" });
        Assert.Equal(HttpStatusCode.OK, registerFcmResponse.StatusCode);

        var unregisterFcmResponse = await userClient.PostAsJsonAsync("/users/unregister_fcm_token", new { Item1 = "fcm-token-1" });
        Assert.Equal(HttpStatusCode.OK, unregisterFcmResponse.StatusCode);

        var passwordResponse = await userClient.PatchAsJsonAsync("/users/password", "NewPassword123!");
        Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);

        var reauthenticateResponse = await anonymousClient.PostAsJsonAsync("/users/authenticate", new
        {
            Email = user.Email,
            Password = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, reauthenticateResponse.StatusCode);

        var deleteResponse = await userClient.DeleteAsync("/users/user");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deletedLookupResponse = await adminClient.GetAsync($"/users/{user.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedLookupResponse.StatusCode);
    }

    [Fact]
    public async Task GetByIdAndProfilePictureEndpoints_RequireAssociationOrAdmin()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var other = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var contactUser = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var sharedUser = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var admin = await _factory.CreateUserAsync(UniqueEmail(), "Password123!", role: Role.Admin);

        await _factory.AddContactAsync(contactUser, owner);
        var sharedList = await _factory.CreateListAsync(owner, "Association list");
        await _factory.AddListPermissionAsync(owner, sharedUser, sharedList.SyncId, ShoppingListPermissionType.Read);

        using var ownerClient = _factory.CreateAuthenticatedClient(owner);
        using var otherClient = _factory.CreateAuthenticatedClient(other);
        using var contactClient = _factory.CreateAuthenticatedClient(contactUser);
        using var sharedClient = _factory.CreateAuthenticatedClient(sharedUser);
        using var adminClient = _factory.CreateAuthenticatedClient(admin);

        var ownerLookupResponse = await ownerClient.GetAsync($"/users/{owner.Id}");
        Assert.Equal(HttpStatusCode.OK, ownerLookupResponse.StatusCode);

        var forbiddenLookupResponse = await otherClient.GetAsync($"/users/{owner.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenLookupResponse.StatusCode);

        var adminLookupResponse = await adminClient.GetAsync($"/users/{owner.Id}");
        Assert.Equal(HttpStatusCode.OK, adminLookupResponse.StatusCode);

        var unknownLookupResponse = await adminClient.GetAsync($"/users/{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, unknownLookupResponse.StatusCode);

        var pictureBytes = new byte[] { 1, 2, 3, 4, 5 };
        var uploadContent = CreateProfilePictureContent(pictureBytes);
        var uploadResponse = await ownerClient.PostAsync("/users/profile_picture", uploadContent);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var transformationResponse = await ownerClient.PostAsJsonAsync("/users/profile_picture_transformation", new
        {
            X = 9,
            Y = 8,
            Scale = 1.5,
            Rotation = 0.25
        });
        Assert.Equal(HttpStatusCode.OK, transformationResponse.StatusCode);

        var lastChangeResponse = await ownerClient.GetAsync("/users/profile_picture_lastchange");
        Assert.Equal(HttpStatusCode.OK, lastChangeResponse.StatusCode);
        using (var lastChangeDocument = await lastChangeResponse.Content.ReadJsonDocumentAsync())
        {
            Assert.Contains(lastChangeDocument.RootElement.EnumerateArray(), element => element.GetProperty("userId").GetString() == owner.Id);
        }

        await AssertProfilePictureAccessAsync(ownerClient, owner.Id, HttpStatusCode.OK, expectBytes: pictureBytes);
        await AssertProfilePictureAccessAsync(contactClient, owner.Id, HttpStatusCode.OK, expectBytes: pictureBytes);
        await AssertProfilePictureAccessAsync(sharedClient, owner.Id, HttpStatusCode.OK, expectBytes: pictureBytes);
        await AssertProfilePictureAccessAsync(adminClient, owner.Id, HttpStatusCode.OK, expectBytes: pictureBytes);
        await AssertProfilePictureAccessAsync(otherClient, owner.Id, HttpStatusCode.Forbidden);

        var deletePictureResponse = await ownerClient.DeleteAsync("/users/profile_picture");
        Assert.Equal(HttpStatusCode.OK, deletePictureResponse.StatusCode);
    }

    private static MultipartFormDataContent CreateProfilePictureContent(byte[] pictureBytes)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            X = 1,
            Y = 2,
            Scale = 1.25,
            Rotation = 0.0,
            LastChangeTransformationTime = DateTime.UtcNow,
            LastChangeImageFileTime = DateTime.UtcNow
        });

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pictureBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "profile.jpg");
        content.Add(new StringContent(metadata), "jsonString");
        return content;
    }

    private static object CreateSocialRegistrationPayload(string route, string email)
    {
        return route switch
        {
            "/users/register_apple" => new
            {
                Item1 = new
                {
                    Email = email,
                    Name = "Apple User",
                    UserId = $"apple-{Guid.NewGuid():N}"
                },
                Item2 = "Password123!"
            },
            "/users/register_google" => new
            {
                Item1 = new
                {
                    Id = $"google-{Guid.NewGuid():N}",
                    Name = "Google User",
                    Email = email
                },
                Item2 = "google-access-token",
                Item3 = "Password123!"
            },
            "/users/register_facebook" => new
            {
                Item1 = new
                {
                    Email = email,
                    Id = $"facebook-{Guid.NewGuid():N}",
                    FirstName = "Face",
                    LastName = "Book"
                },
                Item2 = "facebook-access-token",
                Item3 = "Password123!"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, null)
        };
    }

    private static async Task AssertProfilePictureAccessAsync(HttpClient client, string userId, HttpStatusCode expectedStatusCode, byte[]? expectBytes = null)
    {
        var infoResponse = await client.GetAsync($"/users/profile_picture_info/{userId}");
        Assert.Equal(expectedStatusCode, infoResponse.StatusCode);

        var pictureResponse = await client.GetAsync($"/users/profile_picture/{userId}");
        Assert.Equal(expectedStatusCode, pictureResponse.StatusCode);

        if (expectedStatusCode != HttpStatusCode.OK)
        {
            return;
        }

        using (var infoDocument = await infoResponse.Content.ReadJsonDocumentAsync())
        {
            var properties = GetProperties(infoDocument.RootElement);
            Assert.Equal(9, properties["x"].GetInt32());
            Assert.Equal(8, properties["y"].GetInt32());
        }

        Assert.Equal("image/jpeg", pictureResponse.Content.Headers.ContentType?.MediaType);
        var actualBytes = await pictureResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectBytes, actualBytes);
    }

    private static void AssertSafeUserPayload(JsonElement userElement, bool expectToken)
    {
        var properties = GetProperties(userElement);
        Assert.True(properties.ContainsKey("id"));
        Assert.True(properties.ContainsKey("email"));
        Assert.True(properties.ContainsKey("username"));
        Assert.True(properties.ContainsKey("isverified"));
        Assert.Equal(expectToken, properties.ContainsKey("token"));
        Assert.False(properties.ContainsKey("passwordhash"));
        Assert.False(properties.ContainsKey("salt"));
        Assert.False(properties.ContainsKey("role"));
        Assert.False(properties.ContainsKey("externalid"));
        Assert.False(properties.ContainsKey("contactshareid"));
    }

    private static Dictionary<string, JsonElement> GetProperties(JsonElement element)
    {
        return element.EnumerateObject().ToDictionary(property => property.Name.ToLowerInvariant(), property => property.Value);
    }

    private static string UniqueEmail()
    {
        return $"user-{Guid.NewGuid():N}@example.test";
    }
}
