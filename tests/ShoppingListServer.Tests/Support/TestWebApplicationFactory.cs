using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using ShoppingListServer;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;
using ShoppingListServer.Tests.Support;

namespace ShoppingListServer.Tests.Support;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "integration-test-secret-for-shopping-list-server";

    private readonly string _tempRoot;
    private readonly string _databasePath;
    private readonly string _dataStoragePath;

    public TestWebApplicationFactory()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "shopping-list-server-tests", Guid.NewGuid().ToString("N"));
        _databasePath = Path.Combine(_tempRoot, "shopping-list-tests.db");
        _dataStoragePath = Path.Combine(_tempRoot, "data");
    }

    public FakeEMailVerificationService EmailVerificationService => Services.GetRequiredService<FakeEMailVerificationService>();

    public FakeResetPasswordService ResetPasswordService => Services.GetRequiredService<FakeResetPasswordService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSQLite"] = "true",
                ["UseFirebase"] = "false",
                ["AppSettings:Secret"] = JwtSecret,
                ["AppSettings:DataStorageFolder"] = _dataStoragePath,
                ["AppSettings:UserStorageFolder"] = "users",
                ["AppSettings:NoReplyEMailHost"] = "localhost",
                ["AppSettings:NoReplyEMailPort"] = "25",
                ["AppSettings:NoReplyEMailAddress"] = "noreply@example.test",
                ["AppSettings:NoReplyEMailPassword"] = "password",
                ["AppSettings:DbServerAddress"] = "",
                ["AppSettings:DbServerAddressDocker"] = "",
                ["AppSettings:DbName"] = "shopping-list-tests",
                ["AppSettings:DbUser"] = "",
                ["AppSettings:DbPassword"] = "",
                ["AppSettings:UseHttpsRedirect"] = "False",
                ["AppSettings:UseDocker"] = "False",
                ["AppSettings:FacebookAppID"] = "test-facebook-app",
                ["AppSettings:AppleClientID"] = "test-apple-client",
                ["AppSettings:AppleSignInKeyId"] = "test-apple-key",
                ["AppSettings:AppleTeamId"] = "test-apple-team",
                ["AppSettings:AppleSignInP8SecretResourcePath"] = "auth/AuthKey_HQW3PG254R.p8",
                ["AppSettings:AppleSignInP8SecretPath"] = "auth/AuthKey_HQW3PG254R.p8",
                ["AppSettings:EnableSensitiveDataLogging"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            Directory.CreateDirectory(_tempRoot);
            Directory.CreateDirectory(_dataStoragePath);

            services.RemoveAll(typeof(DbContextOptions<AppDb>));
            services.RemoveAll(typeof(AppDb));
            services.AddDbContextPool<AppDb>(options =>
            {
                options.UseLazyLoadingProxies();
                options.UseSqlite($"Data Source={_databasePath}");
            });

            services.RemoveAll<IAuthenticationService>();
            services.RemoveAll<IEMailVerificationService>();
            services.RemoveAll<IResetPasswordService>();
            services.RemoveAll<IPushNotificationService>();
            services.RemoveAll<IUserHub>();
            services.RemoveAll<IShoppingHub>();

            services.AddSingleton<IAuthenticationService, FakeAuthenticationService>();
            services.AddSingleton<FakeEMailVerificationService>();
            services.AddSingleton<IEMailVerificationService>(provider => provider.GetRequiredService<FakeEMailVerificationService>());
            services.AddSingleton<FakeResetPasswordService>();
            services.AddSingleton<IResetPasswordService>(provider => provider.GetRequiredService<FakeResetPasswordService>());
            services.AddSingleton<IPushNotificationService, NoOpPushNotificationService>();
            services.AddSingleton<IUserHub, NoOpUserHub>();
            services.AddSingleton<IShoppingHub, NoOpShoppingHub>();
        });
    }

    public HttpClient CreateAuthenticatedClient(TestUser user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(user));
        return client;
    }

    public string CreateToken(TestUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(JwtSecret);
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        });

        return tokenHandler.WriteToken(token);
    }

    public async Task<TestUser> CreateUserAsync(
        string email,
        string password,
        string role = Role.User,
        bool isVerified = true,
        string? username = null,
        string? firstName = "Test",
        string? lastName = "User")
    {
        _ = Services;

        using var scope = Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var user = new User
        {
            EMail = email,
            Username = username ?? email.Split('@')[0],
            FirstName = firstName,
            LastName = lastName
        };

        if (!userService.AddUser(user, password))
        {
            throw new InvalidOperationException($"Failed to add test user {email}.");
        }

        var dbUser = db.Users.Single(x => x.Id == user.Id);
        dbUser.Role = role;
        dbUser.IsVerified = isVerified;
        db.SaveChanges();

        return new TestUser(dbUser.Id, dbUser.EMail, password, dbUser.Username, dbUser.Role);
    }

    public async Task AddContactAsync(TestUser sourceUser, TestUser targetUser, UserContactType type = UserContactType.Default)
    {
        _ = Services;

        using var scope = Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        await userService.AddOrUpdateContact(sourceUser.Id, new User { Id = targetUser.Id }, type, true);
    }

    public async Task<ShoppingList> CreateListAsync(TestUser owner, string? name = null)
    {
        _ = Services;

        using var scope = Services.CreateScope();
        var shoppingService = scope.ServiceProvider.GetRequiredService<IShoppingService>();
        var list = new ShoppingList
        {
            Name = name ?? "Weekly Shopping",
            Category = "Groceries",
            DateString = string.Empty,
            Notes = "Seeded notes",
            ProductList = new List<GenericProduct>()
        };

        if (!await shoppingService.AddList(list, owner.Id))
        {
            throw new InvalidOperationException("Failed to add test shopping list.");
        }

        return list;
    }

    public async Task AddListPermissionAsync(TestUser owner, TestUser targetUser, string listId, ShoppingListPermissionType permission)
    {
        _ = Services;

        using var scope = Services.CreateScope();
        var shoppingService = scope.ServiceProvider.GetRequiredService<IShoppingService>();
        var updated = await shoppingService.AddOrUpdateListPermission(owner.Id, targetUser.Id, listId, permission, true, true);
        if (!updated)
        {
            throw new InvalidOperationException("Failed to add test list permission.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
        }
    }
}

public sealed record TestUser(string Id, string Email, string Password, string Username, string Role);
