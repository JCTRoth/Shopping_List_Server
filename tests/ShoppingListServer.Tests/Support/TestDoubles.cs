using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Tests.Support;

public sealed class FakeAuthenticationService : IAuthenticationService
{
    public Task<HttpResponseMessage> AuthenticateFacebook(FacebookProfile facebookProfile, string accessToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public Task<HttpResponseMessage> AuthenticateFacebookWithException(FacebookProfile facebookProfile, string accessToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public Task<HttpResponseMessage> AuthenticateGoogle(GoogleUser googleUser, string accessToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public Task<HttpResponseMessage> AuthenticateGoogleWithException(GoogleUser googleUser, string accessToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public Task<HttpResponseMessage> AuthenticateApple(AppleAccount appleAccount) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public Task<HttpResponseMessage> AuthenticateAppleWithException(AppleAccount appleAccount) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}

public sealed class FakeEMailVerificationService : IEMailVerificationService
{
    private readonly ConcurrentDictionary<string, string> _verificationCodesByUserId = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public FakeEMailVerificationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<bool> SendEMailVerificationCodeAndAddToken(string userId)
    {
        _verificationCodesByUserId[userId] = $"verify-{Guid.NewGuid():N}";
        return Task.FromResult(true);
    }

    public string GetCodeForUser(string userId)
    {
        return _verificationCodesByUserId[userId];
    }

    public User VerifyEMailUrlCode(string urlCode)
    {
        var userId = _verificationCodesByUserId.FirstOrDefault(entry => entry.Value == urlCode).Key;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = db.Users.SingleOrDefault(x => x.Id == userId);
        if (user == null)
        {
            return null;
        }

        user.IsVerified = true;
        db.SaveChanges();
        return user;
    }
}

public sealed class FakeResetPasswordService : IResetPasswordService
{
    private readonly ConcurrentDictionary<string, string> _codesByEmail = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public FakeResetPasswordService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<bool> SendResetPasswordEMailAndAddToken(string email)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (!db.Users.Any(user => user.EMail == email))
        {
            return Task.FromResult(false);
        }

        _codesByEmail[email] = $"reset-{Guid.NewGuid():N}";
        return Task.FromResult(true);
    }

    public string GetCodeForEmail(string email)
    {
        return _codesByEmail[email];
    }

    public bool CheckIfCodeExistsWithException(string email, string code)
    {
        return _codesByEmail.TryGetValue(email, out var expectedCode) && expectedCode == code;
    }

    public bool SetPassword(string email, string code, string newPassword)
    {
        if (!CheckIfCodeExistsWithException(email, code))
        {
            return false;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = db.Users.SingleOrDefault(x => x.EMail == email);
        if (user == null)
        {
            return false;
        }

        var passwordAccess = PasswordAccess.Generate(newPassword);
        user.Salt = passwordAccess.Salt;
        user.PasswordHash = passwordAccess.PasswordHash;
        db.SaveChanges();
        return true;
    }
}

public sealed class NoOpPushNotificationService : IPushNotificationService
{
    public Task SendListAdded(User thisUser, User targetUser, string listId) => Task.CompletedTask;

    public Task SendListAdded(User thisUser, string listId, ShoppingListPermissionType permission) => Task.CompletedTask;
}

public sealed class NoOpUserHub : IUserHub
{
    public Task SendUserVerified(User user) => Task.CompletedTask;

    public Task SendContactAdded(string currentUserId, User contactUser) => Task.CompletedTask;
}

public sealed class NoOpShoppingHub : IShoppingHub
{
    public Task SendListAdded(User user, ShoppingList list, ShoppingListPermissionType permission) => Task.CompletedTask;

    public Task SendListAdded(User thisUser, User targetUser, ShoppingList list) => Task.CompletedTask;

    public Task SendListUpdated(User user, ShoppingList list, ShoppingListPermissionType permission) => Task.CompletedTask;

    public Task SendListPropertyChanged(User user, string listSyncId, string propertyName, string propertyValue, ShoppingListPermissionType permission) => Task.CompletedTask;

    public Task SendListRemoved(User user, string listSyncId, ShoppingListPermissionType permission) => Task.CompletedTask;

    public Task SendListRemoved(User user, string listSyncId, string targetUserId) => Task.CompletedTask;

    public Task<bool> SendListPermissionChanged(User thisUser, User targetUser, string listSyncId, ShoppingListPermissionType permission) => Task.FromResult(true);

    public Task<bool> SendListPermissionRemoved(User thisUser, User targetUser, string listSyncId) => Task.FromResult(true);

    public Task<bool> SendItemNameChanged(User user, string newItemName, string oldItemName, string listSyncId, ShoppingListPermissionType permission) => Task.FromResult(true);

    public Task<bool> SendItemAddedOrUpdated(User user, GenericItem item, string listSyncId, ShoppingListPermissionType permission) => Task.FromResult(true);

    public Task<bool> SendProductAddedOrUpdated(User user, GenericProduct product, string listSyncId, ShoppingListPermissionType permission) => Task.FromResult(true);
}
