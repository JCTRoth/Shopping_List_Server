using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Models.ShoppingData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IShoppingService
    {
        string GetID();
        /// <summary>
        /// Returns the list with the given id. First checks if the given owner has
        /// read permission.
        /// </summary>
        /// <param name="userId">Id of the user who tries to access the shopping list.</param>
        /// <param name="shoppingListId">Id of the searched shopping list</param>
        /// <returns>The shoppinglist with the given id</returns>
        /// <exception cref="NoShoppingListPermissionException">If the user with the given id has no read permission for the list.</exception>
        ShoppingList GetList(string userId, string shoppingListId);
        List<ShoppingListWithPermissionDTO> GetLists(string userId);
        // Return all lists that the user has the given permission for.
        List<ShoppingList> GetLists(string userId, ShoppingListPermissionType permission);
        Task<bool> AddList(ShoppingList list, string userID);
        Task<bool> UpdateList(ShoppingList list, string userId);
        // Updates the lists property with the given name.
        // \param listSyncId - syncd id of the list that is to be updated.
        // \param userId - user id of the user that tries to perform this action.
        // \param propertyName - property to be updates. Currently supported are "Date" and "Notes"
        Task<bool> UpdateListProperty(string listSyncId, string userId, string propertyName, string propertyValue);
        // Only delets the list if
        // - deleteForEveryone==true and user with userId has permission
        // - this is the last user
        Task<bool> DeleteList(string shoppingListId, string thisUserId, string targetUserId);
        Task<bool> DeleteListForEveryone(string shoppingListId, string userId);
        Task<bool> Update_Item_In_List(string itemNameOld, GenericItem itemNew, string userId, string shoppingListId);
        Task<bool> Add_Or_Update_Product_In_List(GenericProduct productNew, string userId, string shoppingListId);
        Task<bool> Remove_Item_In_List(string itemName, string userId, string shoppingListId);

        // Returns all permissions that are assigned to a list.
        // \return List<Tuple<UserId, ShoppingListPermissionType>>
        List<Tuple<string, ShoppingListPermissionType>> GetListPermissions(string shoppingListId);

        // Returns all list permissions of a certain user. These are all lists that a user can at least read.
        // \return List<Tuple<ShoppingListId, ShoppingListPermissionType>>
        List<Tuple<string, ShoppingListPermissionType>> GetUserListPermissions(string userId);

        // Return permission that the given user has for the given list
        // Exception: If this user has no read access to the list.
        ShoppingListPermissionType GetUserListPermission(string shoppingListId, string thisUserId, string userId);

        Task<bool> AddOrUpdateListPermission(string thisUserId, string targetUserId, string shoppingListId, ShoppingListPermissionType permission, bool checkPermission, bool allowUpdate);
        // Remove the permission of a user from a shopping list. Doesn't work if the user is the owner.

        Task<bool> RemoveListPermission(string thisUserId, string targetUserId, string shoppingListId);

        List<string> GetUsersWithPermissions(string listSyncId, ShoppingListPermissionType permission);

        /// <summary>
        /// Generates a token of this lists share id. The share id can be used to share
        /// this list with other users, by them calling <see cref="AddListFromListShareId(string, string)"/>
        /// The share id is stored in <see cref="ShoppingList.ShareId"/>
        /// The token does not expire.
        /// </summary>
        /// <param name="listSyncId">Id of the list that's to be shared</param>
        /// <param name="currentUserId">Id of the currently logged in user</param>
        /// <returns>The share id.</returns>
        public string GenerateOrExtendListShareId(string listSyncId, string currentUserId);

        /// <summary>
        /// Adds a <see cref="ShoppingListPermissionType.WriteAndModifyPermission"/> permission for the
        /// current user to the list with the given listShareId.
        /// 
        /// The list share id has to be generated with <see cref="GenerateOrExtendListShareId(string)"/>.
        /// 
        /// Possible status messages:
        /// <see cref="StatusMessages.ShoppingListNotFound">If there is no shopping list with the given sync id.</see>
        /// <see cref="StatusMessages.ListShareLinkExpired">If the link has been expired (older than 2 days).</see>
        /// <see cref="StatusMessages.ListAlreadyAdded">If the user that created the link doesn't exist.</see>
        /// </summary>
        /// <returns>The added list.</returns>
        public Task<ShoppingList> AddListFromListShareId(string currentUserId, string listShareId);

    }
}
