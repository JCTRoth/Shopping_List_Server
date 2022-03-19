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

            Task<bool> AddOrUpdateListPermission(string thisUserId, string targetUserId, string shoppingListId, ShoppingListPermissionType permission, bool checkPermission);
            // Remove the permission of a user from a shopping list. Doesn't work if the user is the owner.

            Task<bool> RemoveListPermission(string thisUserId, string targetUserId, string shoppingListId);

            List<string> GetUsersWithPermissions(string listSyncId, ShoppingListPermissionType permission);

        }
}
