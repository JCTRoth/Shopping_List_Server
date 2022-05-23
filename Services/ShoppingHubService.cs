using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{

    public class ShoppingHubService : IShoppingHub
    {
        private readonly IHubContext<UpdateHub_Controller> _hubContext;
        private readonly Lazy<IShoppingService> _shoppingService;

        public ShoppingHubService(
            IServiceProvider services,
            IHubContext<UpdateHub_Controller> hubContext)
        {
            _shoppingService = new Lazy<IShoppingService>(() => (IShoppingService)services.GetService(typeof(IShoppingService)));
            _hubContext = hubContext;
        }

        /*
         * 
         * SINGAL R LIVE UPDATES
         * 
        */
        public async Task SendListAdded(User user, ShoppingList list, ShoppingListPermissionType permission)
        {
            string userJson = user == null ? "" : JsonConvert.SerializeObject(user.WithoutPassword());
            string listJson = JsonConvert.SerializeObject(list);
            List<string> users = GetUsersWithPermissionsFiltered(user, list.SyncId, permission);
            await _hubContext.Clients.Users(users).SendAsync("ListAdded", userJson, listJson);
        }

        // Send the given list to all users that have the given permission on that list, e.g.
        // if permission == Read then it's send to all users that have read permission on that list.
        public async Task SendListUpdated(User user, ShoppingList list, ShoppingListPermissionType permission)
        {
            string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
            string listJson = JsonConvert.SerializeObject(list);
            List<string> users = GetUsersWithPermissionsFiltered(user, list.SyncId, permission);
            await _hubContext.Clients.Users(users).SendAsync("ListUpdated", userJson, listJson);
        }

        public async Task SendListPropertyChanged(User user, string listSyncId, string propertyName, string propertyValue, ShoppingListPermissionType permission)
        {
            string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
            List<string> users = GetUsersWithPermissionsFiltered(user, listSyncId, permission);
            await _hubContext.Clients.Users(users).SendAsync("ListPropertyChanged", userJson, listSyncId, propertyName, propertyValue);
        }

        public async Task SendListRemoved(User user, string listSyncId, ShoppingListPermissionType permission)
        {
            string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
            List<string> users = GetUsersWithPermissionsFiltered(user, listSyncId, permission);
            await _hubContext.Clients.Users(users).SendAsync("ListRemoved", userJson, listSyncId);
        }

        public async Task SendListRemoved(User user, string listSyncId, string userId)
        {
            string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
            await _hubContext.Clients.Users(userId).SendAsync("ListRemoved", userJson, listSyncId);
        }

        // Inform the given user that its permission for the given list changed.
        public async Task<bool> SendListPermissionChanged(
            User thisUser,
            string listSyncId,
            string targetUserId,
            ShoppingListPermissionType permission)
        {
            try
            {
                string userJson = thisUser == null ? "" : JsonConvert.SerializeObject(thisUser.WithoutPassword());
                await _hubContext.Clients.Users(targetUserId).SendAsync("ListPermissionChanged", userJson, listSyncId, permission);
                return true;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("SendListPermissionChanged {0}", ex);
                return false;
            }
        }

        public async Task<bool> SendItemNameChanged(
            User user,
            string newItemName,
            string oldItemName,
            string listSyncId,
            ShoppingListPermissionType permission)
        {
            try
            {
                string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
                List<string> users = GetUsersWithPermissionsFiltered(user, listSyncId, permission);
                await _hubContext.Clients.Users(users).SendAsync("ItemNameChanged", userJson, listSyncId, newItemName, oldItemName);
                return true;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("SendItemNameChanged {0}", ex);
                return false;
            }
        }

        public async Task<bool> SendItemAddedOrUpdated(
            User user,
            GenericItem item,
            string listSyncId,
            ShoppingListPermissionType permission)
        {
            try
            {
                string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
                string itemJson = JsonConvert.SerializeObject(item);
                List<string> users = GetUsersWithPermissionsFiltered(user, listSyncId, permission);
                await _hubContext.Clients.Users(users).SendAsync("ItemAddedOrUpdated", userJson, listSyncId, itemJson);
                return true;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("SendItemAddedOrUpdated {0}", ex);
                return false;
            }
        }

        public async Task<bool> SendProductAddedOrUpdated(
            User user,
            GenericProduct product,
            string listSyncId,
            ShoppingListPermissionType permission)
        {
            try
            {
                string userJson = JsonConvert.SerializeObject(user.WithoutPassword());
                string productJson = JsonConvert.SerializeObject(product);
                List<string> users = GetUsersWithPermissionsFiltered(user, listSyncId, permission);
                await _hubContext.Clients.Users(users).SendAsync("ProductAddedOrUpdated", userJson, listSyncId, productJson);
                return true;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("SendProductAddedOrUpdatedn {0}", ex);
                return false;
            }

        }

        private List<string> GetUsersWithPermissionsFiltered(User filteredUser, string listSyncId, ShoppingListPermissionType permission)
        {
            List<string> users = _shoppingService.Value.GetUsersWithPermissions(listSyncId, permission);
            if (filteredUser != null)
                users.Remove(filteredUser.Id);
            return users;
        }
    }
}