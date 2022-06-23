using FirebaseAdmin.Messaging;
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
            var fcmTokens = from list in _db.Set<ShoppingList>()
                            join perm in _db.Set<ShoppingListPermission>() on list.SyncId equals perm.ShoppingListId
                            join user in _db.Set<User>() on thisUser.Id equals user.Id
                            join contact in _db.Set<UserContact>() on new { X1 = user.Id, X2 = list.Owner.Id } equals new { X1 = contact.UserSourceId, X2 = contact.UserTargetId }
                            into contactGroup
                            from contact in contactGroup.DefaultIfEmpty()
                            where !thisUser.Id.Equals(user.Id) &&
                                  listId.Equals(list.SyncId) &&
                                  perm.PermissionType.HasFlag(permission) &&
                                  !string.IsNullOrEmpty(user.FcmToken) &&
                                  (contact == null || list.Owner == null || (contact.UserTargetId.Equals(list.Owner.Id) && contact.UserContactType != UserContactType.Ignored))
                            select user.FcmToken;

            //var fcmTokens = from l in _db.Set<ShoppingList>()
            //                join perm in _db.Set<ShoppingListPermission>() on l.SyncId equals perm.ShoppingListId
            //                join u in _db.Set<User>() on perm.UserId equals u.Id
            //                where !thisUser.Id.Equals(u.Id) && listId.Equals(l.SyncId) && perm.PermissionType.HasFlag(permission) && !string.IsNullOrEmpty(u.FcmToken)
            //                select u.FcmToken;

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
                Notification = new FirebaseAdmin.Messaging.Notification()
                {
                    Title = title,
                    Body = body
                },
                // Set priority to hight so that:
                // Trigger a notification when phone is sleeping.
                // Trigger a heads-up notification when phone is active.
                // https://developer.android.com/guide/topics/ui/notifiers/notifications#Heads-up
                // https://developer.android.com/training/notify-user/build-notification#Priority
                Android = new AndroidConfig()
                {
                    Priority = FirebaseAdmin.Messaging.Priority.High,
                    Notification = new AndroidNotification()
                    {
                        Icon = "ic_stat_logo_shopping_cart_white"
                    }
                },
                // ios high priority notification:
                // Trigger a notification when phone is sleeping.
                // Trigger a heads-up notification when phone is active.
                // https://firebase.google.com/docs/cloud-messaging/send-message?hl=en
                // https://firebase.google.com/docs/reference/admin/node/firebase-admin.messaging.messagingoptions
                Apns = new ApnsConfig()
                {
                    Aps = new Aps()
                    {
                        Sound = "default"
                    }
                },
                Token = fcmToken
            };

            // Send a message to the device corresponding to the provided fcmToken.
            return await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
