using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShoppingListServer.Database;
using ShoppingListServer.Exceptions;
using ShoppingListServer.Helpers;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{

    public class ShoppingService : IShoppingService //, IShoppingHub
    {
        private readonly IUserService _userService;
        private readonly IShoppingHub _hubService;
        private readonly IShoppingListStorageService _storageService;
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;

        public ShoppingService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserService userService,
            IShoppingHub hubService,
            IShoppingListStorageService storageService)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _hubService = hubService;
            _userService = userService;
            _storageService = storageService;
        }

        public string GetID()
        {
            string new_id = Guid.NewGuid().ToString();
            return new_id;
        }

        public ShoppingList GetList(string userId, string shoppingListId)
        {
            ShoppingList list = GetShoppingListEntity(shoppingListId);
            if (list == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(list, userId, ShoppingListPermissionType.Read);
            return LoadShoppingList(shoppingListId);
        }

        public List<ShoppingList> GetLists(string userId, ShoppingListPermissionType permission)
        {
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(userId) && perm.PermissionType.HasFlag(permission)
                        select list;

            List<ShoppingList> listEntities = query.ToList();
            List<ShoppingList> lists = new List<ShoppingList>();
            foreach (ShoppingList listEntity in listEntities)
            {
                ShoppingList list = LoadShoppingList(listEntity.SyncId);
                if (list != null)
                    lists.Add(list);
            }
            return lists;
        }

        public List<ShoppingListWithPermissionDTO> GetLists(string userId)
        {
            // How to get tuple out of querry: https://stackoverflow.com/a/33545601
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(userId)
                        select new ShoppingListWithPermissionDTO( list, perm.UserId, perm.PermissionType );
            return query.ToList();
            //return _db.ShoppingLists.Where(list => CheckPermission(list, userId, permission)).ToList();
        }

        // Adds the given list to this server.
        // Sets list.Id and list.ShoppingListPermissions
        public async Task<bool> AddList(ShoppingList list, string userID)
        {
            ShoppingList existingList = GetShoppingListEntity(list.SyncId);
            if (existingList != null)
            {
                return false;
            }
            else
            {
                list.SyncId = Guid.NewGuid().ToString();
                list.ShoppingListPermissions = new List<ShoppingListPermission>();
                list.ShoppingListPermissions.Add(new ShoppingListPermission()
                {
                    PermissionType = ShoppingListPermissionType.All,
                    ShoppingList = list,
                    UserId = userID
                });
                
                if (_storageService.Store_ShoppingList(userID, list))
                {
                    _db.ShoppingLists.Add(list);
                    _db.SaveChanges();
                    await _hubService.SendListAdded(_userService.GetById(userID), list, ShoppingListPermissionType.Read);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        // Overwrites the given shopping list with the stored one that has the same Id.
        // Throws ShoppingListNotFoundException if there is no such list stored. Use AddList in that case first.
        public async Task<bool> UpdateList(ShoppingList list, string userId)
        {
            ShoppingList listEntity = GetShoppingListEntity(list.SyncId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(list.SyncId);
            CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Write);
            bool success = UpdateShoppingList(list);
            if (success)
            {
                await _hubService.SendListUpdated(_userService.GetById(userId), list, ShoppingListPermissionType.Read);
            }
            return success;
        }

        public async Task<bool> UpdateListProperty(string listSyncId, string userId, string propertyName, string propertyValue)
        {
            ShoppingList listEntity = GetShoppingListEntity(listSyncId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(listSyncId);
            CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Write);

            ShoppingList list = LoadShoppingList(listSyncId);
            if (propertyName == "Date")
                list.DateString = propertyValue;
            else if (propertyName == "Notes")
                list.Notes = propertyValue;

            bool success = UpdateShoppingList(list);
            if (success)
            {
                await _hubService.SendListPropertyChanged(_userService.GetById(userId), listSyncId, propertyName, propertyValue, ShoppingListPermissionType.Read);
            }
            return success;
        }

        public async Task<bool> DeleteList(string shoppingListId, string userId)
        {
            
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Delete);
            _db.ShoppingLists.Remove(listEntity);
            _db.SaveChanges();
            bool success = DeleteShoppingList(shoppingListId);
            if (success)
            {
                await _hubService.SendListRemoved(_userService.GetById(userId), shoppingListId, ShoppingListPermissionType.Read);
            }
            return success;
        }

        // Updates itemNameOld with itemNew.
        // If there is no itemNameOld, does noting.
        public async Task<bool> Update_Item_In_List(string itemNameOld, GenericItem itemNew, string thisUserId, string shoppingListId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);
            ShoppingList list = LoadShoppingList(shoppingListId);

            bool success = false;
            if (list != null)
            {
                int index = list.ProductList.FindIndex(prod => prod.Item.Name == itemNameOld);
                if (index != -1)
                {
                    list.ProductList[index].Item = itemNew;
                    success = UpdateShoppingList(list);
                    if (success)
                    {
                        if (itemNew.Name.Equals(itemNameOld))
                        {
                            await _hubService.SendItemAddedOrUpdated(_userService.GetById(thisUserId), itemNew, list.SyncId, ShoppingListPermissionType.Read);
                        }
                        else
                        {
                            await _hubService.SendItemNameChanged(_userService.GetById(thisUserId), itemNew.Name, itemNameOld, list.SyncId, ShoppingListPermissionType.Read);
                        }
                    }
                }
            }
            return success;
        }

        // Updates the given product. Only product information should change, nothing from the item, like the name.
        // If the product is not part of the list, it is added.
        public async Task<bool> Add_Or_Update_Product_In_List(GenericProduct productNew, string thisUserId, string shoppingListId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);
            ShoppingList list = LoadShoppingList(shoppingListId);

            bool success = false;
            if (list != null)
            {
                int index = list.ProductList.FindIndex(prod => prod.Item.Name == productNew.Item.Name);
                if (index != -1)
                {
                    list.ProductList[index] = productNew;
                }
                else
                {
                    list.ProductList.Add(productNew);
                }
                success = UpdateShoppingList(list);
                if (success)
                {
                    await _hubService.SendProductAddedOrUpdated(_userService.GetById(thisUserId), productNew, list.SyncId, ShoppingListPermissionType.Read);
                }
            }
            return success;
        }

        public async Task<bool> Remove_Item_In_List(string itemName, string thisUserId, string shoppingListId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);
            ShoppingList list = LoadShoppingList(shoppingListId);

            bool success = false;
            if (list != null)
            {
                int index = list.ProductList.FindIndex(prod => prod.Item.Name == itemName);
                if (index != -1)
                {
                    list.ProductList.RemoveAt(index);
                    success = UpdateShoppingList(list);
                    if (success)
                    {
                        await _hubService.SendListUpdated(_userService.GetById(thisUserId), list, ShoppingListPermissionType.Read);
                    }
                }
            }
            return false;
        }

        // Return List<Tuple<UserId, Permission>>
        public List<Tuple<string, ShoppingListPermissionType>> GetListPermissions(string shoppingListId)
        {
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.ShoppingListId.Equals(shoppingListId)
                        select new Tuple<string, ShoppingListPermissionType>(perm.UserId, perm.PermissionType);
            return query.ToList();
        }

        // Return List<Tuple<ShoppingListId, Permission>>
        public List<Tuple<string, ShoppingListPermissionType>> GetUserListPermissions(string userId)
        {
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(userId)
                        select new Tuple<string, ShoppingListPermissionType>(list.SyncId, perm.PermissionType);
            return query.ToList();
        }

        public ShoppingListPermissionType GetUserListPermission(string shoppingListId, string thisUserId, string targetUserId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Read);

            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(targetUserId) && perm.ShoppingListId.Equals(shoppingListId)
                        select perm.PermissionType;
            return query.ToList().FirstOrDefault();
        }

        public List<string> GetUsersWithPermissions(string listSyncId, ShoppingListPermissionType permission)
        {
            List<Tuple<string, ShoppingListPermissionType>> tuples = GetListPermissions(listSyncId);
            List<string> users = new List<string>();
            foreach (Tuple<string, ShoppingListPermissionType> tuple in tuples)
            {
                if (tuple.Item2.HasFlag(permission))
                {
                    users.Add(tuple.Item1);
                }
            }
            return users;
        }

        // Adds the given list permission to target user. Performed by thisUser (thisUser needs permission to change permissions of the given list!)
        // If target use has no permission yet, they get a message that a new list was added instead of just a permission change.
        // \param thisUser - the user who tries to change the permission
        // \param targetUser - the user whose permission should be changed
        // \param shoppingListId - id of the list whose permission is changed
        // \param permission - target permission type
        public async Task<bool> AddOrUpdateListPermission(string thisUserId, string targetUserId, string shoppingListId, ShoppingListPermissionType permission)
        {
            ShoppingList targetList = GetShoppingListEntity(shoppingListId);
            if (targetList == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyAccess);

            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(targetUserId) && perm.ShoppingListId.Equals(shoppingListId)
                        select new { list, perm };
            var first = query.FirstOrDefault();
            if (first != null)
            {
                first.perm.PermissionType = permission;
            }
            else
            {
                ShoppingList list = GetShoppingListEntity(shoppingListId);
                if (list == null)
                    throw new ShoppingListNotFoundException(shoppingListId);
                list.ShoppingListPermissions.Add(new ShoppingListPermission
                {
                    ShoppingListId = shoppingListId,
                    UserId = targetUserId,
                    PermissionType = permission
                });
            }
            _db.SaveChanges();
            if (first != null)
            {
                await _hubService.SendListPermissionChanged(_userService.GetById(thisUserId), shoppingListId, targetUserId, permission);
            }
            else
            {
                targetList = LoadShoppingList(shoppingListId);
                await _hubService.SendListAdded(_userService.GetById(thisUserId), targetList, ShoppingListPermissionType.Read);
            }
            return true;
        }

        // Remove the list permission of a given user.
        // \param thisUser - the user that removes the list permission
        // \param targetUserId - the user whoms permission is removed
        // \param shoppingListId - the list ids permission that is changed.
        public async Task<bool> RemoveListPermission(string thisUserId, string targetUserId, string shoppingListId)
        {
            ShoppingList targetList = GetShoppingListEntity(shoppingListId);
            if (targetList == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyAccess);

            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(targetUserId) && perm.ShoppingListId.Equals(shoppingListId)
                        select new { list, perm };
            var first = query.FirstOrDefault();
            bool success = false;
            if (first != null)
            {
                first.list.ShoppingListPermissions.Remove(first.perm);
                _db.SaveChanges();

                // Remove the list for the user whose permission was removed.
                await _hubService.SendListRemoved(_userService.GetById(thisUserId), shoppingListId, targetUserId);
                success = true;
            }
            return success;
        }

        // Returns the entity of the given shopping list. This is an object that has only the fields set that are in the database.
        // Fields that are marked with [NotMapped] will be null. This is only the "hull" of a list that should be used for query
        // operations on the database or to fetch the missing information from json files.
        private ShoppingList GetShoppingListEntity(string shoppingListId)
        {
            return _db.ShoppingLists.FirstOrDefault(ShoppingList => ShoppingList.SyncId == shoppingListId);
        }

        private ShoppingListPermission GetPermission(ShoppingList list, string userId)
        {
            var permissions = list.ShoppingListPermissions.Where(per => per.UserId == userId).ToList();
            return permissions.FirstOrDefault();
        }

        private bool CheckPermission(ShoppingListPermission permission, ShoppingListPermissionType expectedPermission)
        {
            return permission != null && permission.PermissionType.HasFlag(expectedPermission);
        }

        private bool CheckPermission(ShoppingList list, string userId, ShoppingListPermissionType expectedPermission)
        {
            return CheckPermission(GetPermission(list, userId), expectedPermission);
        }

        private void CheckPermissionWithException(ShoppingList list, string userId, ShoppingListPermissionType expectedPermission)
        {
            ShoppingListPermission permission = GetPermission(list, userId);
            if (!CheckPermission(permission, expectedPermission))
                throw new NoShoppingListPermissionException(permission, expectedPermission);
        }

        private string GetOwnerId(string shoppingListId)
        {
            var query = from perm in _db.Set<ShoppingListPermission>()
                        where perm.ShoppingListId == shoppingListId && perm.PermissionType.HasFlag(ShoppingListPermissionType.All)
                        select perm.UserId;
            var owner = query.FirstOrDefault();
            return owner;
        }

        // Loads the json file of the shopping list with the given id.
        private ShoppingList LoadShoppingList(string shoppingListId)
        {
            string ownerId = GetOwnerId(shoppingListId);
            if (ownerId != null)
            {
                return _storageService.Load_ShoppingList(ownerId, shoppingListId);
            }
            return null;
        }

        private bool DeleteShoppingList(string shoppingListId)
        {
            string ownerId = GetOwnerId(shoppingListId);
            bool success = false;
            if (ownerId != null)
            {
                success = _storageService.Delete_ShoppingList(ownerId, shoppingListId);
            }
            return success;
        }

        // Stores the updated json file.
        // \param list - the updated list
        private bool UpdateShoppingList(ShoppingList list)
        {
            string ownerId = GetOwnerId(list.SyncId);
            bool success = false;
            if (ownerId != null)
            {
                success = _storageService.Store_ShoppingList(ownerId, list);
            }
            return success;
        }
    }
}