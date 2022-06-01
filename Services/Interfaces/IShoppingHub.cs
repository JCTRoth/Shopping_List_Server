using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShoppingListServer.LiveUpdates
{
    public interface IShoppingHub
    {
        Task SendListAdded(User user, ShoppingList list, ShoppingListPermissionType permission);

        // Send the given list to all users that have the given permission on that list, e.g.
        // if permission == Read then it's send to all users that have read permission on that list.
        Task SendListUpdated(User user, ShoppingList list, ShoppingListPermissionType permission);

        Task SendListPropertyChanged(User user, string listSyncId, string propertyName, string propertyValue, ShoppingListPermissionType permission);

        /// <summary>
        /// Inform everyone that has the given permission to the given list that the list has been removed
        /// for them. (their permission has been removed). This should result in them removing their
        /// list locally.
        /// </summary>
        /// <param name="user">The user that removed the list. They are not informed.</param>
        /// <param name="listSyncId">List that was removed.</param>
        /// <param name="permission">Permission of users that are to be informed.</param>
        /// <returns></returns>
        Task SendListRemoved(User user, string listSyncId, ShoppingListPermissionType permission);

        /// <summary>
        /// Inform the user with <see cref="targetUserId"/> that the list was removed for them.
        /// </summary>
        /// <param name="user">The user that removed the list for them.</param>
        /// <param name="listSyncId">Id of the removed list</param>
        /// <param name="targetUserId">The user that was removed from the list</param>
        /// <returns></returns>
        Task SendListRemoved(User user, string listSyncId, string targetUserId);

        /// <summary>
        /// Inform the given user that its permission for the given list changed.
        /// </summary>
        Task<bool> SendListPermissionChanged(
            User thisUser,
            User targetUser,
            string listSyncId,
            ShoppingListPermissionType permission);

        /// <summary>
        /// Inform everyone that has access to the list with listSyncId, that the user with targetUserId
        /// has no longer access permission to the list.
        /// </summary>
        Task<bool> SendListPermissionRemoved(
            User thisUser,
            User targetUser,
            string listSyncId);

        Task<bool> SendItemNameChanged(
            User user,
            string newItemName,
            string oldItemName,
            string listSyncId,
            ShoppingListPermissionType permission);

        Task<bool> SendItemAddedOrUpdated(
            User user,
            GenericItem item,
            string listSyncId,
            ShoppingListPermissionType permission);

        Task<bool> SendProductAddedOrUpdated(
            User user,
            GenericProduct product,
            string listSyncId,
            ShoppingListPermissionType permission);
    }
}

