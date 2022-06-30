using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Exceptions;
using ShoppingListServer.Helpers;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Logic;
using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{

    public class ShoppingService : IShoppingService //, IShoppingHub
    {
        private readonly IUserService _userService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IShoppingHub _hubService;
        private readonly IShoppingListStorageService _storageService;
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;

        public ShoppingService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserService userService,
            IPushNotificationService pushNotificationService,
            IShoppingHub hubService,
            IShoppingListStorageService storageService)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _hubService = hubService;
            _pushNotificationService = pushNotificationService;
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
            if (list.Owner != null)
                CheckIfListBlockedWithException(userId, list.Owner.Id);
            return LoadShoppingList(shoppingListId);
        }

        public ListLastChangeTimeDTO GetListLastChangeTime(string userId, string shoppingListId)
        {
            ShoppingList list = GetShoppingListEntity(shoppingListId);
            if (list == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            CheckPermissionWithException(list, userId, ShoppingListPermissionType.Read);
            return new ListLastChangeTimeDTO(shoppingListId, list.LastChangeServerTime);
        }

        public List<ShoppingList> GetLists(string userId, ShoppingListPermissionType permission)
        {
            // the thing with .DefaultIfEmpty() and the contactGroup is a way of formulating a left outer join in linq,
            // see https://docs.microsoft.com/en-us/dotnet/csharp/linq/perform-left-outer-joins
            // In this case, it is not necessary to have a contact but if there is one, don't include entries where the target user
            // is blocked by this user.
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>() on new { X1 = list.SyncId, X2 = userId } equals new { X1 = perm.ShoppingListId, X2 = perm.UserId }
                        join contact in _db.Set<UserContact>() on new { X1 = userId, X2 = list.Owner.Id } equals new { X1 = contact.UserSourceId, X2 = contact.UserTargetId }
                        into contactGroup
                        from contact in contactGroup.DefaultIfEmpty()
                        where perm.PermissionType.HasFlag(permission) &&
                              (contact == null || list.Owner == null || (contact.UserTargetId.Equals(list.Owner.Id) && contact.UserContactType != UserContactType.Ignored))
                        select list;

            List<ShoppingList> listEntities = query.ToList();
            List<ShoppingList> lists = new List<ShoppingList>();
            foreach (ShoppingList listEntity in listEntities)
            {
                ShoppingList list = LoadShoppingList(listEntity.SyncId);

                if (list != null)
                {
                    lists.Add(list);
                }
            }
            return lists;
        }

        public List<ShoppingListWithPermissionDTO> GetListsWithPermission(string userId, ShoppingListPermissionType permission)
        {
            // How to get tuple out of querry: https://stackoverflow.com/a/33545601
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>() on new { X1 = list.SyncId, X2 = userId } equals new { X1 = perm.ShoppingListId, X2 = perm.UserId }
                        join contact in _db.Set<UserContact>() on new { X1 = userId, X2 = list.Owner.Id } equals new { X1 = contact.UserSourceId, X2 = contact.UserTargetId }
                        into contactGroup
                        from contact in contactGroup.DefaultIfEmpty()
                        where perm.PermissionType.HasFlag(permission) &&
                              (contact == null || contact.UserContactType != UserContactType.Ignored)
                        select new ShoppingListWithPermissionDTO( list, perm.UserId, perm.PermissionType );
            return query.ToList();
        }

        public List<ListLastChangeTimeDTO> GetListsLastChangeTimes(string userId, ShoppingListPermissionType permission)
        {
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>() on new { X1=list.SyncId, X2=userId } equals new { X1=perm.ShoppingListId, X2=perm.UserId }
                        join contact in _db.Set<UserContact>() on new { X1 = userId, X2 = list.Owner.Id } equals new { X1=contact.UserSourceId, X2=contact.UserTargetId }
                        into contactGroup
                        from contact in contactGroup.DefaultIfEmpty()
                        where perm.PermissionType.HasFlag(permission) &&
                              (contact == null || contact.UserContactType != UserContactType.Ignored)
                        select new ListLastChangeTimeDTO(list.SyncId, list.LastChangeServerTime);

            return query.ToList();
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
                list.UpdateLastChangeServerTime();
                list.Owner = _userService.GetById(userID);
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
                    User user = _userService.GetById(userID);
                    await _hubService.SendListAdded(user, list, ShoppingListPermissionType.Read);
                    await _pushNotificationService.SendListAdded(user, list.SyncId, ShoppingListPermissionType.Read);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        // Overwrites the stored shopping list with the given one that has the same Id.
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

        // Updates the lists property with the given name.
        // \param listSyncId - syncd id of the list that is to be updated.
        // \param userId - user id of the user that tries to perform this action.
        // \param propertyName - property to be updates. Currently supported are "Date" and "Notes"
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

        public async Task<bool> DeleteList(string shoppingListId, string thisUserId, string targetUserId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            bool success = await RemoveListPermission(thisUserId, targetUserId, shoppingListId);
            if (success)
            {
                // Send to target user
                await _hubService.SendListRemoved(_userService.GetById(thisUserId), shoppingListId, targetUserId);
                await _hubService.SendListPermissionRemoved(_userService.GetById(thisUserId), _userService.GetById(targetUserId), shoppingListId);
            }

            return success;
        }

        public async Task<bool> DeleteListForEveryone(string shoppingListId, string userId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);

            CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Delete);
            bool success = DeleteListWithoutChecks(shoppingListId);
            if (success)
            {
                // Send to all with permission Read
                await _hubService.SendListRemoved(_userService.GetById(userId), shoppingListId, ShoppingListPermissionType.Read);
            }
            _db.SaveChanges();
            return success;
        }

        // Just delets the list without doing any checks, e.g. if this user has
        // permssion to do so or there are still permissions left.
        private bool DeleteListWithoutChecks(string shoppingListId)
        {
            ShoppingList listEntity = GetShoppingListEntity(shoppingListId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(shoppingListId);
            bool success = DeleteShoppingListJson(shoppingListId);
            _db.ShoppingLists.Remove(listEntity);
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
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>() on list.SyncId equals perm.ShoppingListId
                        join user in _db.Set<User>() on perm.UserId equals user.Id
                        join contact in _db.Set<UserContact>() on new { X1 = user.Id, X2 = list.Owner.Id } equals new { X1 = contact.UserSourceId, X2 = contact.UserTargetId }
                        into contactGroup
                        from contact in contactGroup.DefaultIfEmpty()
                        where listSyncId == list.SyncId && 
                              perm.PermissionType.HasFlag(permission) &&
                              (contact == null || contact.UserContactType != UserContactType.Ignored)
                        select user.Id;

            return query.ToList();
        }

        public List<string> GetUsersWithPermissionsFiltered(string filteredUserId, string listSyncId, ShoppingListPermissionType permission)
        {
            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>() on list.SyncId equals perm.ShoppingListId
                        join user in _db.Set<User>() on perm.UserId equals user.Id
                        join contact in _db.Set<UserContact>() on new { X1 = user.Id, X2 = list.Owner.Id } equals new { X1 = contact.UserSourceId, X2 = contact.UserTargetId }
                        into contactGroup
                        from contact in contactGroup.DefaultIfEmpty()
                        where listSyncId == list.SyncId &&
                              filteredUserId != user.Id &&
                              perm.PermissionType.HasFlag(permission) &&
                              (contact == null || contact.UserContactType != UserContactType.Ignored)
                        select user.Id;

            return query.ToList();
        }

        // Adds the given list permission to target user. Performed by thisUser (thisUser needs permission to change permissions of the given list!)
        // If target use has no permission yet, they get a message that a new list was added instead of just a permission change.
        // \param thisUser - the user who tries to change the permission
        // \param targetUser - the user whose permission should be changed
        // \param shoppingListId - id of the list whose permission is changed
        // \param permission - target permission type
        public async Task<bool> AddOrUpdateListPermission(
            string thisUserId, string targetUserId, string shoppingListId, ShoppingListPermissionType permission, bool checkPermission = true, bool allowUpdate = true)
        {
            ShoppingList targetList = GetShoppingListEntity(shoppingListId);
            if (targetList == null)
                throw new ShoppingListNotFoundException(shoppingListId);

            // TODO: actually move a list if the owner is changed with that method

            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(targetUserId) && perm.ShoppingListId.Equals(shoppingListId)
                        select new { list, perm };
            var first = query.FirstOrDefault();
            if (first != null)
            {
                if (!allowUpdate)
                    throw new Exception(StatusMessages.ListAlreadyAdded);
                if (!string.IsNullOrEmpty(thisUserId) && checkPermission)
                    CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyPermission);
                first.perm.PermissionType = permission;
            }
            else
            {
                if (!string.IsNullOrEmpty(thisUserId) && checkPermission)
                    CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.AddPermission);
                targetList.ShoppingListPermissions.Add(new ShoppingListPermission
                {
                    ShoppingListId = shoppingListId,
                    UserId = targetUserId,
                    PermissionType = permission
                });
            }
            targetList.UpdateLastChangeServerTime();
            _db.SaveChanges();
            if (first == null)
            {
                targetList = LoadShoppingList(shoppingListId);
                User user = _userService.GetById(thisUserId);
                User targetUser = _userService.GetById(targetUserId);
                await _hubService.SendListPermissionChanged(user, targetUser, shoppingListId, permission);
                if (user != targetUser) // Only send notificaiton to target user if they haven't added the list themselfs (e.g. via app-link)
                {
                    await _hubService.SendListAdded(user, targetUser, targetList);
                    await _pushNotificationService.SendListAdded(user, targetUser, targetList.SyncId);
                }
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

            // ModifyAccess permission is only required if the permission of another user is changed.
            // Everyone can always remove their own permission.
            if (!thisUserId.Equals(targetUserId))
                CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyPermission);

            var query = from list in _db.Set<ShoppingList>()
                        join perm in _db.Set<ShoppingListPermission>()
                            on list.SyncId equals perm.ShoppingListId
                        where perm.UserId.Equals(targetUserId) && perm.ShoppingListId.Equals(shoppingListId)
                        select new { list, perm };
            var first = query.FirstOrDefault();
            bool success = false;
            if (first != null)
            {
                success = first.list.ShoppingListPermissions.Remove(first.perm);
                targetList.UpdateLastChangeServerTime();
                _db.SaveChanges();

                if (first.list.ShoppingListPermissions.Count == 0)
                {
                    // If this was the last user that had this list assigned, remove it for good.
                    DeleteListWithoutChecks(shoppingListId);
                }
                // The list Admin (person with all permissions) can change. There needs
                // to be at least one admin so when one is removed, check if there is another one and
                // if there isn't, assign someone. If there was no admin left, noone would be able to remove users
                // from the list anymore.
                else if (first.perm.PermissionType == ShoppingListPermissionType.All)
                {
                    bool containsAdmin = first.list.ShoppingListPermissions.Any(perm => perm.PermissionType == ShoppingListPermissionType.All);
                    if (!containsAdmin)
                    {
                        string newAdmin = first.list.ShoppingListPermissions.FirstOrDefault().UserId;
                        await AddOrUpdateListPermission(thisUserId, newAdmin, shoppingListId, ShoppingListPermissionType.All, false, true);

                        // List owners don't change. Even if the owner has no access permissions to his own
                        // list, they remain the owner of the list. This is done to avoid moving lists
                        // around to new folders.
                        // If the remove user was the owner and there are still other users left,
                        // assign a different user and move the list to the new owners folder.
                        // _storageService.Move_ShoppingList(targetUserId, newOwner, shoppingListId);
                    }
                }

                // Remove the list for the user whose permission was removed.
                await _hubService.SendListRemoved(_userService.GetById(thisUserId), shoppingListId, targetUserId);
                await _hubService.SendListPermissionRemoved(_userService.GetById(thisUserId), _userService.GetById(targetUserId), shoppingListId);
                _db.SaveChanges();
                success = true;
            }
            return success;
        }

        public string GenerateOrExtendListShareId(string listSyncId, string currentUserId)
        {
            ShoppingList listEntity = GetShoppingListEntity(listSyncId);
            if (listEntity == null)
                throw new ShoppingListNotFoundException(listSyncId);
            CheckPermissionWithException(listEntity, currentUserId, ShoppingListPermissionType.AddPermission);

            if (listEntity.ShareId == null || listEntity.ShareId.IsExpired())
            {
                listEntity.ShareId = new ExpirationToken()
                {
                    ExpirationTime = DateTime.MaxValue,
                    Data = RandomKeyFactory.GetUniqueKeyOriginal_BIASED(32)
                };
            }
            _db.SaveChanges();
            return listEntity.ShareId.Data;
        }

        public async Task<ShoppingList> AddListFromListShareId(string currentUserId, string listShareId)
        {
            var query = from list in _db.Set<ShoppingList>()
                        where list.ShareId != null && list.ShareId.Data == listShareId
                        select list;
            ShoppingList targetList = query.FirstOrDefault();
            if (targetList == null)
            {
                throw new Exception(StatusMessages.ShoppingListNotFound);
            }
            else if (targetList.ShareId.IsExpired())
            {
                throw new Exception(StatusMessages.ListShareLinkExpired);
            }
            else
            {
                await AddOrUpdateListPermission(currentUserId, currentUserId, targetList.SyncId, ShoppingListPermissionType.WriteAndAddPermission, false, false);
            }
            return targetList;
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

        private bool CheckIfListBlocked(string userId, string listOwnerId)
        {
            var blockedQuery = from userContact in _db.Set<UserContact>()
                               where userContact.UserSourceId == userId &&
                                     userContact.UserTargetId == listOwnerId &&
                                     userContact.UserContactType == UserContactType.Ignored
                               select userContact;
            return blockedQuery.FirstOrDefault() != null;
        }

        private void CheckIfListBlockedWithException(string userId, string listOwnerId)
        {
            bool isBlocked = CheckIfListBlocked(userId, listOwnerId);
            if (isBlocked)
                throw new Exception(StatusMessages.ListIsOwnedByBlockedUser);
        }

        private string GetOwnerId(string shoppingListId)
        {
            var queryList = from l in _db.Set<ShoppingList>()
                        where l.SyncId == shoppingListId
                        select l;
            var list = queryList.FirstOrDefault();
            if (list.Owner == null)
            {
                var queryOwner = from perm in _db.Set<ShoppingListPermission>()
                            where perm.ShoppingListId == shoppingListId && perm.PermissionType.HasFlag(ShoppingListPermissionType.All)
                            select perm.UserId;
                var owner = queryOwner.FirstOrDefault();
                return owner;
            }
            return list.Owner.Id;
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

        private bool DeleteShoppingListJson(string shoppingListId)
        {
            string ownerId = GetOwnerId(shoppingListId);
            bool success = false;
            if (ownerId != null)
            {
                success = _storageService.Delete_ShoppingList(ownerId, shoppingListId);
            }
            return success;
        }

        /// <summary>
        /// Stores the given list as json file. The list must contain all the item information.
        /// It is not enough to pass a database list entity here.
        /// </summary>
        /// <param name="list">the updated list</param>
        /// <returns></returns>
        private bool UpdateShoppingList(ShoppingList list)
        {
            string ownerId = GetOwnerId(list.SyncId);
            bool success = false;
            if (ownerId != null)
            {
                success = _storageService.Store_ShoppingList(ownerId, list);
                if (success)
                {
                    ShoppingList listEntity = GetShoppingListEntity(list.SyncId);
                    listEntity.UpdateLastChangeServerTime();
                    _db.SaveChanges();
                }
            }
            return success;
        }
    }
}