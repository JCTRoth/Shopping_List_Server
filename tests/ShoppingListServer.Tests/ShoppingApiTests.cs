using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests;

public sealed class ShoppingApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ShoppingApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListLifecycleEndpoints_Work()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var ownerClient = _factory.CreateAuthenticatedClient(owner);

        var addListResponse = await ownerClient.PostAsJsonAsync("/shopping/list", CreateShoppingListPayload("Weekly list"));
        Assert.Equal(HttpStatusCode.OK, addListResponse.StatusCode);

        var createdList = await addListResponse.Content.ReadJsonDocumentAsync();
        var syncId = createdList.RootElement.GetProperty("syncId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(syncId));

        var listsResponse = await ownerClient.GetAsync("/shopping/lists");
        Assert.Equal(HttpStatusCode.OK, listsResponse.StatusCode);
        using (var listsDocument = await listsResponse.Content.ReadJsonDocumentAsync())
        {
            Assert.Contains(listsDocument.RootElement.EnumerateArray(), element => element.GetProperty("syncId").GetString() == syncId);
        }

        var patchPropertyResponse = await ownerClient.PatchAsJsonAsync("/shopping/listproperty", new
        {
            Item1 = syncId,
            Item2 = "Notes",
            Item3 = "Updated notes"
        });
        Assert.Equal(HttpStatusCode.OK, patchPropertyResponse.StatusCode);

        var addProductResponse = await ownerClient.PatchAsJsonAsync("/shopping/product", new
        {
            ShoppingListId = syncId,
            NewProduct = new
            {
                Item = new { Name = "Bread" },
                Count = 2,
                Checked = false
            }
        });
        Assert.Equal(HttpStatusCode.OK, addProductResponse.StatusCode);

        var renameItemResponse = await ownerClient.PatchAsJsonAsync("/shopping/item", new
        {
            ShoppingListId = syncId,
            OldItemName = "Bread",
            NewItem = new { Name = "Whole Grain Bread" }
        });
        Assert.Equal(HttpStatusCode.OK, renameItemResponse.StatusCode);

        var removeItemResponse = await ownerClient.DeleteAsJsonAsync("/shopping/item", new
        {
            ShoppingListId = syncId,
            ItemName = "Whole Grain Bread"
        });
        Assert.Equal(HttpStatusCode.OK, removeItemResponse.StatusCode);

        var listResponse = await ownerClient.GetAsync($"/shopping/list/{syncId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using (var listDocument = await listResponse.Content.ReadJsonDocumentAsync())
        {
            Assert.Equal("Updated notes", listDocument.RootElement.GetProperty("notes").GetString());
            Assert.Empty(listDocument.RootElement.GetProperty("productList").EnumerateArray());
        }

        var lastChangeResponse = await ownerClient.GetAsync($"/shopping/list_lastchange/{syncId}");
        Assert.Equal(HttpStatusCode.OK, lastChangeResponse.StatusCode);

        var deleteListResponse = await ownerClient.DeleteAsync($"/shopping/list/{syncId}?deleteForEveryone=true");
        Assert.Equal(HttpStatusCode.OK, deleteListResponse.StatusCode);
    }

    [Fact]
    public async Task PermissionAndShareEndpoints_Work()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var member = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        var shareUser = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var ownerClient = _factory.CreateAuthenticatedClient(owner);
        using var memberClient = _factory.CreateAuthenticatedClient(member);
        using var shareClient = _factory.CreateAuthenticatedClient(shareUser);

        var addListResponse = await ownerClient.PostAsJsonAsync("/shopping/list", CreateShoppingListPayload("Shared list"));
        Assert.Equal(HttpStatusCode.OK, addListResponse.StatusCode);
        using var createdList = await addListResponse.Content.ReadJsonDocumentAsync();
        var syncId = createdList.RootElement.GetProperty("syncId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(syncId));

        var addPermissionResponse = await ownerClient.PutAsJsonAsync("/shopping/listpermission", new
        {
            Item1 = member.Id,
            Item2 = syncId,
            Item3 = "Read"
        });
        Assert.Equal(HttpStatusCode.OK, addPermissionResponse.StatusCode);

        var memberListsResponse = await memberClient.GetAsync("/shopping/lists");
        Assert.Equal(HttpStatusCode.OK, memberListsResponse.StatusCode);
        using (var memberListsDocument = await memberListsResponse.Content.ReadJsonDocumentAsync())
        {
            Assert.Contains(memberListsDocument.RootElement.EnumerateArray(), element => element.GetProperty("syncId").GetString() == syncId);
        }

        var permissionsResponse = await ownerClient.GetAsync($"/shopping/listpermission/{syncId}");
        Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);
        using (var permissionsDocument = await permissionsResponse.Content.ReadJsonDocumentAsync())
        {
            var permissions = permissionsDocument.RootElement.EnumerateArray().ToList();
            Assert.True(permissions.Count >= 2);
            foreach (var permission in permissions)
            {
                var user = permission.GetProperty("user");
                var userProperties = user.EnumerateObject().ToDictionary(property => property.Name.ToLowerInvariant(), property => property.Value);
                Assert.False(userProperties.ContainsKey("token"));
                Assert.False(userProperties.ContainsKey("role"));
                Assert.False(userProperties.ContainsKey("externalid"));
            }
        }

        var memberPermissionResponse = await memberClient.GetAsync($"/shopping/listpermission/{syncId}/{member.Id}");
        Assert.Equal(HttpStatusCode.OK, memberPermissionResponse.StatusCode);
        var memberPermission = await memberPermissionResponse.Content.ReadFromJsonAsync<string>();
        Assert.Equal("Read", memberPermission);

        var userPermissionsResponse = await memberClient.GetAsync("/shopping/listpermission");
        Assert.Equal(HttpStatusCode.OK, userPermissionsResponse.StatusCode);

        var shareIdResponse = await ownerClient.PostEmptyAsync($"/shopping/generate_share_id/{syncId}");
        Assert.Equal(HttpStatusCode.OK, shareIdResponse.StatusCode);
        var shareId = await shareIdResponse.Content.ReadFromJsonAsync<string>();
        Assert.False(string.IsNullOrWhiteSpace(shareId));

        var addByShareIdResponse = await shareClient.PostAsJsonAsync("/shopping/list_share_id", new { Item1 = shareId });
        Assert.Equal(HttpStatusCode.OK, addByShareIdResponse.StatusCode);
        var sharedListId = await addByShareIdResponse.Content.ReadFromJsonAsync<string>();
        Assert.Equal(syncId, sharedListId);

        var resolveShareIdResponse = await shareClient.GetAsync($"/shopping/list_share_id/{shareId}");
        Assert.Equal(HttpStatusCode.OK, resolveShareIdResponse.StatusCode);
        var resolvedListId = await resolveShareIdResponse.Content.ReadFromJsonAsync<string>();
        Assert.Equal(syncId, resolvedListId);

        var removePermissionResponse = await ownerClient.DeleteAsync($"/shopping/listpermission/{syncId}/{member.Id}");
        Assert.Equal(HttpStatusCode.OK, removePermissionResponse.StatusCode);
    }

    private static object CreateShoppingListPayload(string name)
    {
        return new
        {
            Name = name,
            Category = "Groceries",
            DateString = string.Empty,
            Notes = "Initial notes",
            ProductList = Array.Empty<object>()
        };
    }

    private static string UniqueEmail()
    {
        return $"shopping-{Guid.NewGuid():N}@example.test";
    }
}
