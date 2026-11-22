using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShoppingListServer.Database;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests;

/// <summary>
/// Covers the relational list content model: item rows, ordering, whole-list replacement and the
/// absence of any JSON-file storage.
/// </summary>
public sealed class ShoppingListPersistenceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ShoppingListPersistenceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListContent_RoundTripsThroughTheDatabase()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);

        var createResponse = await client.PostAsJsonAsync("/shopping/list", new
        {
            Name = "Weekend",
            Category = "Food",
            DateString = "2026-11-28T00:00:00",
            Notes = "Bring bags",
            ProductList = new object[]
            {
                new
                {
                    Item = new
                    {
                        Name = "Milk",
                        ImagePath = (string?)null,
                        Category = new { Name = "Dairy", ColorHex = "#4FC3F7", ImagePath = (string?)null }
                    },
                    Count = 2,
                    Checked = false
                },
                new
                {
                    Item = new { Name = "Bread" },
                    Count = 0,
                    Checked = true
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createdDocument = await createResponse.Content.ReadJsonDocumentAsync();
        var syncId = createdDocument.RootElement.GetProperty("syncId").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(syncId));

        var listResponse = await client.GetAsync($"/shopping/list/{syncId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = await listResponse.Content.ReadJsonDocumentAsync();
        var list = listDocument.RootElement;

        Assert.Equal("Weekend", list.GetProperty("name").GetString());
        Assert.Equal("Food", list.GetProperty("category").GetString());
        Assert.Equal("2026-11-28T00:00:00", list.GetProperty("dateString").GetString());
        Assert.Equal("Bring bags", list.GetProperty("notes").GetString());

        var products = list.GetProperty("productList").EnumerateArray().ToList();
        Assert.Equal(2, products.Count);
        // Order is the array order that was persisted.
        Assert.Equal("Milk", products[0].GetProperty("item").GetProperty("name").GetString());
        Assert.Equal("Bread", products[1].GetProperty("item").GetProperty("name").GetString());
        // imagePath must be present as null, category must be a full object.
        Assert.Equal(JsonValueKind.Null, products[0].GetProperty("item").GetProperty("imagePath").ValueKind);
        Assert.Equal("Dairy", products[0].GetProperty("item").GetProperty("category").GetProperty("name").GetString());
        Assert.Equal("#4FC3F7", products[0].GetProperty("item").GetProperty("category").GetProperty("colorHex").GetString());
        Assert.Equal(2, products[0].GetProperty("count").GetInt32());
        Assert.True(products[1].GetProperty("checked").GetBoolean());

        // The overview returns full content in a single call.
        var listsResponse = await client.GetAsync("/shopping/lists");
        Assert.Equal(HttpStatusCode.OK, listsResponse.StatusCode);
        using var listsDocument = await listsResponse.Content.ReadJsonDocumentAsync();
        var overview = listsDocument.RootElement.EnumerateArray().Single(element => element.GetProperty("syncId").GetString() == syncId);
        Assert.Equal(2, overview.GetProperty("productList").GetArrayLength());

        // And the items really are rows, ordered by Position.
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var rows = await db.ShoppingListProducts
            .Where(row => row.ShoppingListId == syncId)
            .OrderBy(row => row.Position)
            .ToListAsync();
        Assert.Equal(new[] { "Milk", "Bread" }, rows.Select(row => row.ItemName).ToArray());
        Assert.Equal(new[] { 0, 1 }, rows.Select(row => row.Position).ToArray());
    }

    [Fact]
    public async Task FullListReplace_DropsOmittedItemsAndReorders()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk", "Bread", "Eggs");

        var current = await GetListAsync(client, syncId);
        var replacement = new
        {
            SyncId = syncId,
            Name = "Replaced",
            Category = "Food",
            DateString = string.Empty,
            Notes = "after replace",
            ProductList = new object[]
            {
                ProductElement("Eggs", count: 5),
                ProductElement("Milk", count: 1),
                // "Bread" is omitted on purpose: a whole-list PATCH replaces membership.
                ProductElement("Milk", count: 9)
            }
        };

        var patchResponse = await client.PatchAsJsonAsync("/shopping/list", replacement);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = await GetListAsync(client, syncId);
        var products = updated.GetProperty("productList").EnumerateArray().ToList();
        Assert.Equal(new[] { "Eggs", "Milk" }, products.Select(p => p.GetProperty("item").GetProperty("name").GetString()).ToArray());
        // Duplicate names keep the first occurrence.
        Assert.Equal(5, products[0].GetProperty("count").GetInt32());
        Assert.Equal(1, products[1].GetProperty("count").GetInt32());
        Assert.Equal("Replaced", updated.GetProperty("name").GetString());
        Assert.Equal("after replace", updated.GetProperty("notes").GetString());
        Assert.NotEqual(current.GetProperty("notes").GetString(), updated.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task ProductUpsert_AppendsNewItemsAndUpdatesExistingOnesInPlace()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk");

        var addResponse = await client.PatchAsJsonAsync("/shopping/product", new
        {
            ShoppingListId = syncId,
            NewProduct = ProductElement("Bread", count: 3)
        });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var updateResponse = await client.PatchAsJsonAsync("/shopping/product", new
        {
            ShoppingListId = syncId,
            NewProduct = ProductElement("Milk", count: 7, isChecked: true)
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var list = await GetListAsync(client, syncId);
        var products = list.GetProperty("productList").EnumerateArray().ToList();
        Assert.Equal(new[] { "Milk", "Bread" }, products.Select(p => p.GetProperty("item").GetProperty("name").GetString()).ToArray());
        Assert.Equal(7, products[0].GetProperty("count").GetInt32());
        Assert.True(products[0].GetProperty("checked").GetBoolean());
        Assert.Equal(3, products[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ItemRename_KeepsPositionAndRejectsNameCollisions()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk", "Bread");

        var renameResponse = await client.PatchAsJsonAsync("/shopping/item", new
        {
            ShoppingListId = syncId,
            OldItemName = "Bread",
            NewItem = new { Name = "Whole Grain Bread", Category = new { Name = "Bakery", ColorHex = "#FFCC80" } }
        });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        var renamed = await GetListAsync(client, syncId);
        var products = renamed.GetProperty("productList").EnumerateArray().ToList();
        Assert.Equal(new[] { "Milk", "Whole Grain Bread" }, products.Select(p => p.GetProperty("item").GetProperty("name").GetString()).ToArray());
        Assert.Equal("Bakery", products[1].GetProperty("item").GetProperty("category").GetProperty("name").GetString());

        // Renaming onto an existing name must fail cleanly instead of hitting the unique index.
        var collisionResponse = await client.PatchAsJsonAsync("/shopping/item", new
        {
            ShoppingListId = syncId,
            OldItemName = "Whole Grain Bread",
            NewItem = new { Name = "Milk" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, collisionResponse.StatusCode);

        var afterCollision = await GetListAsync(client, syncId);
        Assert.Equal(2, afterCollision.GetProperty("productList").GetArrayLength());
    }

    [Fact]
    public async Task DeleteItem_RemovesOnlyThatRow_AndDeleteListCascades()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk", "Bread");

        var deleteItemResponse = await client.DeleteAsJsonAsync("/shopping/item", new
        {
            ShoppingListId = syncId,
            ItemName = "Milk"
        });
        Assert.Equal(HttpStatusCode.OK, deleteItemResponse.StatusCode);

        var list = await GetListAsync(client, syncId);
        var products = list.GetProperty("productList").EnumerateArray().ToList();
        Assert.Single(products);
        Assert.Equal("Bread", products[0].GetProperty("item").GetProperty("name").GetString());

        var deleteListResponse = await client.DeleteAsync($"/shopping/list/{syncId}?deleteForEveryone=true");
        Assert.Equal(HttpStatusCode.OK, deleteListResponse.StatusCode);

        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Empty(await db.ShoppingListProducts.Where(row => row.ShoppingListId == syncId).ToListAsync());
        Assert.Null(await db.ShoppingLists.FirstOrDefaultAsync(list => list.SyncId == syncId));
    }

    [Fact]
    public async Task ListProperty_PersistsNotesAndDate_AndIgnoresUnknownProperties()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk");

        var before = await GetVersionAsync(syncId);

        var notesResponse = await client.PatchAsJsonAsync("/shopping/listproperty", new { Item1 = syncId, Item2 = "Notes", Item3 = "new notes" });
        Assert.Equal(HttpStatusCode.OK, notesResponse.StatusCode);

        var dateResponse = await client.PatchAsJsonAsync("/shopping/listproperty", new { Item1 = syncId, Item2 = "Date", Item3 = "2027-01-05T00:00:00" });
        Assert.Equal(HttpStatusCode.OK, dateResponse.StatusCode);

        var afterNotesAndDate = await GetVersionAsync(syncId);
        Assert.True(afterNotesAndDate > before);

        var list = await GetListAsync(client, syncId);
        Assert.Equal("new notes", list.GetProperty("notes").GetString());
        Assert.Equal("2027-01-05T00:00:00", list.GetProperty("dateString").GetString());

        // The web client sends IsEditModeEnabled, which the server never persisted.
        var modeResponse = await client.PatchAsJsonAsync("/shopping/listproperty", new { Item1 = syncId, Item2 = "IsEditModeEnabled", Item3 = "true" });
        Assert.Equal(HttpStatusCode.OK, modeResponse.StatusCode);
        Assert.Equal(afterNotesAndDate, await GetVersionAsync(syncId));
    }

    [Fact]
    public async Task ListOperations_NeverWriteJsonFiles()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk", "Bread");

        await client.PatchAsJsonAsync("/shopping/product", new { ShoppingListId = syncId, NewProduct = ProductElement("Eggs", 1) });
        await client.PatchAsJsonAsync("/shopping/listproperty", new { Item1 = syncId, Item2 = "Notes", Item3 = "no files" });
        await client.GetAsync("/shopping/lists");

        // The list storage used to create {syncId}.json under a data folder; nothing may do that now.
        var testRoot = Path.Combine(Path.GetTempPath(), "shopping-list-server-tests");
        if (Directory.Exists(testRoot))
        {
            var jsonFiles = Directory.GetFiles(testRoot, "*.json", SearchOption.AllDirectories);
            Assert.DoesNotContain(jsonFiles, path => Path.GetFileName(path).Contains(syncId));
        }

        Assert.False(Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "data")));
    }

    [Fact]
    public async Task ConcurrentProductWrites_AllPersist()
    {
        var owner = await _factory.CreateUserAsync(UniqueEmail(), "Password123!");
        using var client = _factory.CreateAuthenticatedClient(owner);
        var syncId = await CreateListAsync(client, "Milk");

        var names = new[] { "Bread", "Eggs", "Butter", "Cheese" };
        var requests = names
            .Select(name => client.PatchAsJsonAsync("/shopping/product", new
            {
                ShoppingListId = syncId,
                NewProduct = ProductElement(name, 1)
            }))
            .ToArray();

        var responses = await Task.WhenAll(requests);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var list = await GetListAsync(client, syncId);
        var productNames = list.GetProperty("productList").EnumerateArray()
            .Select(product => product.GetProperty("item").GetProperty("name").GetString())
            .ToList();

        Assert.Equal(1 + names.Length, productNames.Count);
        Assert.All(names, name => Assert.Contains(name, productNames));
        Assert.Equal(productNames.Count, productNames.Distinct().Count());
    }

    private async Task<string> CreateListAsync(HttpClient client, params string[] itemNames)
    {
        var createResponse = await client.PostAsJsonAsync("/shopping/list", new
        {
            Name = "List",
            Category = "Food",
            DateString = string.Empty,
            Notes = string.Empty,
            ProductList = itemNames.Select(name => ProductElement(name, 0)).ToArray()
        });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        using var document = await createResponse.Content.ReadJsonDocumentAsync();
        return document.RootElement.GetProperty("syncId").GetString()!;
    }

    private static async Task<JsonElement> GetListAsync(HttpClient client, string syncId)
    {
        var response = await client.GetAsync($"/shopping/list/{syncId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await response.Content.ReadJsonDocumentAsync();
        return document.RootElement.Clone();
    }

    private async Task<int> GetVersionAsync(string syncId)
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.ShoppingLists.Where(list => list.SyncId == syncId).Select(list => list.Version).SingleAsync();
    }

    private static object ProductElement(string name, int count, bool isChecked = false)
    {
        return new
        {
            Item = new
            {
                Name = name,
                ImagePath = (string?)null,
                Category = new { Name = "General", ColorHex = "#FFFFFF", ImagePath = (string?)null }
            },
            Count = count,
            Checked = isChecked
        };
    }

    private static string UniqueEmail()
    {
        return $"persistence-{Guid.NewGuid():N}@example.test";
    }
}
