using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private AppDb _db;

        public PushNotificationService(AppDb db)
        {
            _db = db;
        }

        public async Task SendListAdded(User thisUser, User targetUser, string listId)
        {
            if (targetUser != null && !string.IsNullOrEmpty(targetUser.FcmToken))
            {
                await SendListPushNotification(thisUser.Username, listId, targetUser.FcmToken);
            }
        }

        public async Task SendListAdded(User thisUser, string listId, ShoppingListPermissionType permission)
        {
            var fcmTokens = from l in _db.Set<ShoppingList>()
                            join perm in _db.Set<ShoppingListPermission>() on l.SyncId equals perm.ShoppingListId
                            join u in _db.Set<User>() on perm.UserId equals u.Id
                            where !thisUser.Id.Equals(u.Id) && listId.Equals(l.SyncId) && perm.PermissionType.HasFlag(permission) && !string.IsNullOrEmpty(u.FcmToken)
                            select u.FcmToken;

            foreach (var fcmToken in fcmTokens)
            {
                await SendListPushNotification(thisUser.Username, listId, fcmToken);
            }

        }

        private async Task<string> SendListPushNotification(string username, string listId, string fcmToken)
        {
            string title = "ServiceNow";
            string body = string.Format("{0} shared a list with you", username);

            // See documentation on defining a message payload.
            var message = new FirebaseAdmin.Messaging.Message()
            {
                Data = new Dictionary<string, string>()
                {
                    { "title", title },
                    { "body", body },
                    { "list_sync_id", listId },
                    { "sound", "default" }
                },
                Token = fcmToken,
            };

            // Send a message to the device corresponding to the provided fcmToken.
            return await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
