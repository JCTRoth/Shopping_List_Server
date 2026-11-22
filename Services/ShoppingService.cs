using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
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
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;

        public ShoppingService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserService userService,
            IPushNotificationService pushNotificationService,
            IShoppingHub hubService)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _hubService = hubService;
            _pushNotificationService = pushNotificationService;
            _userService = userService;
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

            // One query (split into two statements) for every list and all of its items.
            List<ShoppingList> listEntities = query
                .AsNoTracking()
                .Include(list => list.Products)
                .AsSplitQuery()
                .ToList();

            return listEntities.Select(ShoppingListContentMapper.ToWireCopy).ToList();
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
        // Sets list.SyncId and list.ShoppingListPermissions
        public async Task<bool> AddList(ShoppingList list, string userID)
        {
            if (list == null)
                return false;

            if (!string.IsNullOrEmpty(list.SyncId) && GetShoppingListEntity(list.SyncId) != null)
            {
                return false;
            }

            list.SyncId = Guid.NewGuid().ToString();
            list.UpdateLastChangeServerTime();
            list.Owner = _userService.GetById(userID);
            list.Products = ShoppingListContentMapper.BuildProducts(list.ProductList, list.SyncId);
            list.ShoppingListPermissions = new List<ShoppingListPermission>();
            list.ShoppingListPermissions.Add(new ShoppingListPermission()
            {
                PermissionType = ShoppingListPermissionType.All,
                ShoppingList = list,
                UserId = userID
            });

            // One transaction inserts the list, its items and the owner's permission.
            _db.ShoppingLists.Add(list);
            await _db.SaveChangesAsync();

            User user = _userService.GetById(userID);
            ShoppingList created = ShoppingListContentMapper.ToWire(list);
            await _hubService.SendListAdded(user, created, ShoppingListPermissionType.Read);
            await _pushNotificationService.SendListAdded(user, list.SyncId, ShoppingListPermissionType.Read);
            return true;
        }

        // Overwrites the stored shopping list with the given one that has the same Id.
        // Throws ShoppingListNotFoundException if there is no such list stored. Use AddList in that case first.
        public async Task<bool> UpdateList(ShoppingList list, string userId)
        {
            if (list == null)
                return false;

            bool success = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList listEntity = GetShoppingListWithProducts(list.SyncId);
                if (listEntity == null)
                    throw new ShoppingListNotFoundException(list.SyncId);
                CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Write);

                ShoppingListContentMapper.ApplyContent(list, listEntity);
                listEntity.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return true;
            });

            if (success)
            {
                ShoppingList updated = LoadShoppingList(list.SyncId);
                if (updated != null)
                    await _hubService.SendListUpdated(_userService.GetById(userId), updated, ShoppingListPermissionType.Read);
            }
            return success;
        }

        // Updates the lists property with the given name.
        // \param listSyncId - syncd id of the list that is to be updated.
        // \param userId - user id of the user that tries to perform this action.
        // \param propertyName - property to be updates. Currently supported are "Date" and "Notes"
        public async Task<bool> UpdateListProperty(string listSyncId, string userId, string propertyName, string propertyValue)
        {
            // The server only persists Notes and Date. Anything else (the web client also sends
            // "IsEditModeEnabled", which it stores locally) is deliberately a no-op: no write, no
            // change-stamp bump, no hub event.
            if (propertyName != "Date" && propertyName != "Notes")
                return true;

            string value = propertyValue ?? string.Empty;

            bool success = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList listEntity = GetShoppingListEntity(listSyncId);
                if (listEntity == null)
                    throw new ShoppingListNotFoundException(listSyncId);
                CheckPermissionWithException(listEntity, userId, ShoppingListPermissionType.Write);

                if (propertyName == "Date")
                    listEntity.DateString = value;
                else
                    listEntity.Notes = value;

                listEntity.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return true;
            });

            if (success)
                await _hubService.SendListPropertyChanged(_userService.GetById(userId), listSyncId, propertyName, value, ShoppingListPermissionType.Read);
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
            return await DeleteListForEveryone(listEntity, _userService.GetById(userId));
        }

        private async Task<bool> DeleteListForEveryone(ShoppingList listEntity, User thisUser)
        {
            if (listEntity == null)
                throw new ShoppingListNotFoundException(string.Empty);

            CheckPermissionWithException(listEntity, thisUser.Id, ShoppingListPermissionType.Delete);
            bool success = DeleteListWithoutChecks(listEntity.SyncId);
            if (success)
            {
                // Send to all with permission Read
                await _hubService.SendListRemoved(thisUser, listEntity.SyncId, ShoppingListPermissionType.Read);
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

            // The item rows and permissions are removed by the database cascade.
            _db.ShoppingLists.Remove(listEntity);
            return true;
        }

        public async Task<bool> DeleteAllListsOfUser(string thisUserId, bool deleteForEveryone)
        {
            List<ShoppingListWithPermissionDTO> listsWithPermissions = GetListsWithPermission(thisUserId, ShoppingListPermissionType.Read);
            User thisUser = _userService.GetById(thisUserId);
            bool success = true;
            foreach (ShoppingListWithPermissionDTO listWithPermission in listsWithPermissions)
            {
                if (deleteForEveryone && listWithPermission.Permission.HasFlag(ShoppingListPermissionType.Delete))
                    success &= await DeleteListForEveryone(listWithPermission.List, thisUser);
                else
                    success &= await DeleteList(listWithPermission.List.SyncId, thisUserId, thisUserId);
            }
            return success;
        }

        // Updates itemNameOld with itemNew.
        // If there is no itemNameOld, does noting.
        public async Task<bool> Update_Item_In_List(string itemNameOld, GenericItem itemNew, string thisUserId, string shoppingListId)
        {
            (bool Success, bool NameChanged) outcome = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList listEntity = GetShoppingListWithProducts(shoppingListId);
                if (listEntity == null)
                    throw new ShoppingListNotFoundException(shoppingListId);
                CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);

                ShoppingListProduct row = listEntity.Products.FirstOrDefault(product => product.ItemName == itemNameOld);
                if (row == null)
                    return (false, false);

                string newName = itemNew?.Name ?? string.Empty;
                bool nameChanged = !string.Equals(newName, itemNameOld, StringComparison.Ordinal);
                if (nameChanged && listEntity.Products.Any(product => !ReferenceEquals(product, row) && product.ItemName == newName))
                {
                    // Renaming onto an existing item name would violate the (list, name) unique index.
                    // Report it as a failed update instead of a 500.
                    return (false, false);
                }

                row.ItemName = newName;
                row.ItemImagePath = itemNew?.ImagePath;
                row.CategoryName = itemNew?.Category?.Name ?? string.Empty;
                row.CategoryColorHex = itemNew?.Category?.ColorHex ?? string.Empty;
                row.CategoryImagePath = itemNew?.Category?.ImagePath;

                listEntity.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return (true, nameChanged);
            });

            if (outcome.Success)
            {
                if (outcome.NameChanged)
                {
                    await _hubService.SendItemNameChanged(_userService.GetById(thisUserId), itemNew.Name, itemNameOld, shoppingListId, ShoppingListPermissionType.Read);
                }
                else
                {
                    await _hubService.SendItemAddedOrUpdated(_userService.GetById(thisUserId), itemNew, shoppingListId, ShoppingListPermissionType.Read);
                }
            }
            return outcome.Success;
        }

        // Updates the given product. Only product information should change, nothing from the item, like the name.
        // If the product is not part of the list, it is added.
        public async Task<bool> Add_Or_Update_Product_In_List(GenericProduct productNew, string thisUserId, string shoppingListId)
        {
            if (productNew?.Item == null || string.IsNullOrEmpty(productNew.Item.Name))
                return false;

            string itemName = productNew.Item.Name;

            bool success = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList listEntity = GetShoppingListWithProducts(shoppingListId);
                if (listEntity == null)
                    throw new ShoppingListNotFoundException(shoppingListId);
                CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);

                ShoppingListProduct row = listEntity.Products.FirstOrDefault(product => product.ItemName == itemName);
                if (row == null)
                {
                    int nextPosition = listEntity.Products.Count == 0 ? 0 : listEntity.Products.Max(product => product.Position) + 1;
                    row = new ShoppingListProduct
                    {
                        ShoppingListId = shoppingListId,
                        ItemName = itemName,
                        Position = nextPosition
                    };
                    listEntity.Products.Add(row);
                }

                row.ItemImagePath = productNew.Item.ImagePath;
                row.CategoryName = productNew.Item.Category?.Name ?? string.Empty;
                row.CategoryColorHex = productNew.Item.Category?.ColorHex ?? string.Empty;
                row.CategoryImagePath = productNew.Item.Category?.ImagePath;
                row.Count = productNew.Count;
                row.Checked = productNew.Checked;

                listEntity.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return true;
            });

            if (success)
                await _hubService.SendProductAddedOrUpdated(_userService.GetById(thisUserId), productNew, shoppingListId, ShoppingListPermissionType.Read);
            return success;
        }

        public async Task<bool> Remove_Item_In_List(string itemName, string thisUserId, string shoppingListId)
        {
            bool success = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList listEntity = GetShoppingListWithProducts(shoppingListId);
                if (listEntity == null)
                    throw new ShoppingListNotFoundException(shoppingListId);
                CheckPermissionWithException(listEntity, thisUserId, ShoppingListPermissionType.Write);

                ShoppingListProduct row = listEntity.Products.FirstOrDefault(product => product.ItemName == itemName);
                if (row == null)
                    return false;

                listEntity.Products.Remove(row);
                listEntity.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return true;
            });

            if (success)
            {
                ShoppingList updated = LoadShoppingList(shoppingListId);
                if (updated != null)
                    await _hubService.SendListUpdated(_userService.GetById(thisUserId), updated, ShoppingListPermissionType.Read);
            }
            return success;
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
            // TODO: actually move a list if the owner is changed with that method

            bool isNewPermission = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList targetList = GetShoppingListEntity(shoppingListId);
                if (targetList == null)
                    throw new ShoppingListNotFoundException(shoppingListId);

                ShoppingListPermission existingPermission = targetList.ShoppingListPermissions
                    .FirstOrDefault(entry => entry.UserId.Equals(targetUserId));

                if (existingPermission != null)
                {
                    if (!allowUpdate)
                        throw new Exception(StatusMessages.ListAlreadyAdded);
                    if (!string.IsNullOrEmpty(thisUserId) && checkPermission)
                        CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyPermission);
                    existingPermission.PermissionType = permission;
                    targetList.UpdateLastChangeServerTime();
                    await _db.SaveChangesAsync();
                    return false;
                }

                if (!string.IsNullOrEmpty(thisUserId) && checkPermission)
                    CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.AddPermission);

                targetList.ShoppingListPermissions.Add(new ShoppingListPermission
                {
                    ShoppingListId = shoppingListId,
                    UserId = targetUserId,
                    PermissionType = permission
                });
                targetList.UpdateLastChangeServerTime();
                await _db.SaveChangesAsync();
                return true;
            });

            if (isNewPermission)
            {
                ShoppingList targetList = LoadShoppingList(shoppingListId);
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
            string newAdminUserId = null;

            bool success = await WithConcurrencyRetryAsync(async () =>
            {
                ShoppingList targetList = GetShoppingListEntity(shoppingListId);
                if (targetList == null)
                    throw new ShoppingListNotFoundException(shoppingListId);

                // ModifyAccess permission is only required if the permission of another user is changed.
                // Everyone can always remove their own permission.
                if (!thisUserId.Equals(targetUserId))
                    CheckPermissionWithException(targetList, thisUserId, ShoppingListPermissionType.ModifyPermission);

                ShoppingListPermission permission = targetList.ShoppingListPermissions
                    .FirstOrDefault(entry => entry.UserId.Equals(targetUserId));
                if (permission == null)
                    return false;

                bool wasAdmin = permission.PermissionType == ShoppingListPermissionType.All;
                targetList.ShoppingListPermissions.Remove(permission);
                targetList.UpdateLastChangeServerTime();
                newAdminUserId = null;

                if (targetList.ShoppingListPermissions.Count == 0)
                {
                    // If this was the last user that had this list assigned, remove it for good.
                    _db.ShoppingLists.Remove(targetList);
                    await _db.SaveChangesAsync();
                    return true;
                }

                // The list Admin (person with all permissions) can change. There needs
                // to be at least one admin so when one is removed, check if there is another one and
                // if there isn't, assign someone. If there was no admin left, noone would be able to remove users
                // from the list anymore.
                // List owners don't change. Even if the owner has no access permissions to his own
                // list, they remain the owner of the list.
                if (wasAdmin && !targetList.ShoppingListPermissions.Any(entry => entry.PermissionType == ShoppingListPermissionType.All))
                {
                    newAdminUserId = targetList.ShoppingListPermissions.FirstOrDefault()?.UserId;
                    if (newAdminUserId != null)
                    {
                        targetList.ShoppingListPermissions.Add(new ShoppingListPermission
                        {
                            ShoppingListId = shoppingListId,
                            UserId = newAdminUserId,
                            PermissionType = ShoppingListPermissionType.All
                        });
                    }
                }

                await _db.SaveChangesAsync();
                return true;
            });

            if (success)
            {
                User thisUser = _userService.GetById(thisUserId);
                // Remove the list for the user whose permission was removed.
                await _hubService.SendListRemoved(thisUser, shoppingListId, targetUserId);
                await _hubService.SendListPermissionRemoved(thisUser, _userService.GetById(targetUserId), shoppingListId);
                if (newAdminUserId != null)
                    await _hubService.SendListPermissionChanged(thisUser, _userService.GetById(newAdminUserId), shoppingListId, ShoppingListPermissionType.All);
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
                throw new Exception(StatusMessages.ListNotFound);
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

        public ShoppingList GetListFromListShareId(string listShareId)
        {
            var query = from list in _db.Set<ShoppingList>()
                        where list.ShareId != null && list.ShareId.Data == listShareId
                        select list;
            ShoppingList targetList = query.FirstOrDefault();
            if (targetList == null)
            {
                throw new Exception(StatusMessages.ListNotFound);
            }
            else if (targetList.ShareId.IsExpired())
            {
                throw new Exception(StatusMessages.ListShareLinkExpired);
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

        // Returns the tracked entity of the given shopping list with its item rows loaded, so that
        // changes to those rows are persisted on SaveChanges.
        private ShoppingList GetShoppingListWithProducts(string shoppingListId)
        {
            return _db.ShoppingLists
                .Include(ShoppingList => ShoppingList.Products)
                .FirstOrDefault(ShoppingList => ShoppingList.SyncId == shoppingListId);
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

        // Loads the given shopping list with all of its items from the database and projects it to
        // the wire shape (ProductList populated, strings normalized). Read-only: never tracked.
        private ShoppingList LoadShoppingList(string shoppingListId)
        {
            ShoppingList entity = _db.ShoppingLists
                .AsNoTracking()
                .Include(ShoppingList => ShoppingList.Products)
                .FirstOrDefault(ShoppingList => ShoppingList.SyncId == shoppingListId);

            return ShoppingListContentMapper.ToWireCopy(entity);
        }

        /// <summary>
        /// Runs a mutating operation and retries it from a fresh read when a concurrent writer won
        /// the race (optimistic concurrency) or when a concurrent insert hits the unique
        /// (list, item name) index. The tracker is cleared between attempts so the operation sees the
        /// committed state instead of its own stale entities.
        /// </summary>
        private async Task<T> WithConcurrencyRetryAsync<T>(Func<Task<T>> operation, int maxAttempts = 3)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
                {
                    _db.ChangeTracker.Clear();
                }
                catch (DbUpdateException exception) when (attempt < maxAttempts && IsUniqueConstraintViolation(exception))
                {
                    _db.ChangeTracker.Clear();
                }
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException switch
            {
                PostgresException postgres => postgres.SqlState == "23505",
                SqliteException sqlite => sqlite.SqliteErrorCode == 19,
                _ => false
            };
        }
    }
}