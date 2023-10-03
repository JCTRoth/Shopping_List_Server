using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;
using ShoppingListServer.Logic;

namespace ShoppingListServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class ShoppingController : ControllerBase
    {
        protected IShoppingService _shoppingService;
        protected IUserService _userService;

        public ShoppingController(IShoppingService shoppingService, IUserService userService)
        {
            _shoppingService = shoppingService;
            _userService = userService;
        }

        [Authorize(Roles = Role.User)]
        [HttpGet("list/{syncID}")]
        public IActionResult GetList(string syncID)
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ShoppingList list = _shoppingService.GetList(userID, syncID);
            if (list != null)
                return Ok(list);
            else
                return BadRequest(new { message = "Not Found" });
        }

        [Authorize(Roles = Role.User)]
        [HttpGet("list_lastchange/{syncID}")]
        public IActionResult GetListLastChangeTime(string syncID)
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ListLastChangeTimeDTO listLastChangeTime = _shoppingService.GetListLastChangeTime(userID, syncID);
            return Ok(listLastChangeTime);
        }

        /// <summary>
        /// Returns all lists that the logged in user has access to.
        /// 
        /// Lists whose owner are blocked are not included even if the user has access.
        /// </summary>
        /// <returns>List of ShoppingLists</returns>
        [Authorize(Roles = Role.User)]
        [HttpGet("lists")]
        public IActionResult GetLists()
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<ShoppingList> lists = _shoppingService.GetLists(userID, ShoppingListPermissionType.Read);
            if (lists != null)
                return Ok(lists);
            else
                return BadRequest(new { message = "Not Found" });
        }

        [Authorize(Roles = Role.User)]
        [HttpGet("lists_lastchange")]
        public IActionResult GetListsLastChangeTimes()
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<ListLastChangeTimeDTO> listsLastChangeTimes = _shoppingService.GetListsLastChangeTimes(userID, ShoppingListPermissionType.Read);
            if (listsLastChangeTimes != null)
                return Ok(listsLastChangeTimes);
            else
                return BadRequest(new { message = "Not Found" });
        }

        [Authorize(Roles = Role.User)]
        [HttpPost("list")]
        public async Task<IActionResult> AddList([FromBody] object shoppingList_json_object)
        {
            ShoppingList new_list_item = JsonConvert.DeserializeObject<ShoppingList>(shoppingList_json_object.ToString());
            bool added = await _shoppingService.AddList(new_list_item, User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (added)
                return Ok(new_list_item);
            else
                return BadRequest(StatusMessages.ListAlreadyAdded);
        }

        [Authorize(Roles = Role.User)]
        [HttpDelete("list/{syncId}")]
        public async Task<IActionResult> DeleteList(string syncId, bool? deleteForEveryone = false)
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool deleted = false;
            if (deleteForEveryone.GetValueOrDefault(false))
                deleted = await _shoppingService.DeleteListForEveryone(syncId, userID);
            else
                deleted = await _shoppingService.DeleteList(syncId, userID, userID); // Delete your own permission from the list.
            if (deleted)
                return Ok();
            else
                return BadRequest(new { message = StatusMessages.ListNotFound });
        }

        [HttpPatch("list")]
        public async Task<IActionResult> UpdateList([FromBody] object listJson)
        {
            ShoppingList listIn = JsonConvert.DeserializeObject<ShoppingList>(listJson.ToString());
            bool success = await _shoppingService.UpdateList(listIn, User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (success)
                return Ok();
            else
                return BadRequest(StatusMessages.ListUpdateFailed);
        }

        [Authorize(Roles = Role.User)]
        [HttpPatch("item")]
        public async Task<IActionResult> Update_Item_In_List([FromBody] object update_request_json)
        {
            Update_Item updatelist_command = JsonConvert.DeserializeObject<Update_Item>(update_request_json.ToString());
            bool ok = await _shoppingService.Update_Item_In_List(
                updatelist_command.OldItemName,
                updatelist_command.NewItem,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                updatelist_command.ShoppingListId);

            if (ok)
                return Ok();
            else
                return BadRequest(StatusMessages.ItemNotFound);
        }

        [HttpPatch("listproperty")]
        public async Task<IActionResult> ChangeListProperty([FromBody] object jsonBody)
        {
            Tuple<string, string, string> propertyTuple = JsonConvert.DeserializeObject<Tuple<string, string, string>>(jsonBody.ToString());
            bool ok = await _shoppingService.UpdateListProperty(propertyTuple.Item1, User.FindFirstValue(ClaimTypes.NameIdentifier), propertyTuple.Item2, propertyTuple.Item3);

            if (ok)
                return Ok();
            else
                return BadRequest(StatusMessages.ListUpdateFailed);
        }

        [Authorize(Roles = Role.User)]
        [HttpDelete("item")]
        public async Task<IActionResult> Remove_Item_In_List([FromBody] object update_request_json)
        {
            Remove_Item removeitem_command = JsonConvert.DeserializeObject<Remove_Item>(update_request_json.ToString());
            bool ok = await _shoppingService.Remove_Item_In_List(
                removeitem_command.ItemName,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                removeitem_command.ShoppingListId);

            if (ok)
                return Ok();
            else
                return BadRequest(StatusMessages.ItemNotFound);
        }

        [Authorize(Roles = Role.User)]
        [HttpPatch("product")]
        public async Task<IActionResult> Update_Product_In_List([FromBody] object update_request_json)
        {
            Update_Product updatelist_command = JsonConvert.DeserializeObject<Update_Product>(update_request_json.ToString());
            bool ok = await _shoppingService.Add_Or_Update_Product_In_List(
                updatelist_command.NewProduct,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                updatelist_command.ShoppingListId);

            if (ok)
                return Ok();
            else
                return BadRequest(StatusMessages.ItemNotFound);
        }

        // Returns the permissions of all users of a list:
        // \return List<Tuple<User, string>> => List<Tuple<User, ShoppingListPermissionType>>
        [Authorize(Roles = Role.User)]
        [HttpGet("listpermission/{listId}")]
        public IActionResult GetListPermissions(string listId)
        {
            string userID = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<Tuple<string, ShoppingListPermissionType>> userPermissions = _shoppingService.GetListPermissions(listId);
            if (userPermissions != null)
            {
                List<Tuple<User, string>> permissions = new List<Tuple<User, string>>();
                foreach (Tuple<string, ShoppingListPermissionType> tuple in userPermissions)
                {
                    permissions.Add(Tuple.Create(_userService.GetById(tuple.Item1).WithoutPassword(), tuple.Item2.ToString()));
                }
                return Ok(permissions);
            }
            else
            {
                return BadRequest(new { message = "Not Found" });
            }
        }

        // Returns the permissions that this user has to any list.
        // \return List<Tuple<string, string>> => List<Tuple<ShoppingListId, ShoppingListPermissionType>>
        [Authorize(Roles = Role.User)]
        [HttpGet("listpermission")]
        public IActionResult GetUserListPermissions()
        {
            string thisUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<Tuple<string, ShoppingListPermissionType>> listPermissions =
                _shoppingService.GetUserListPermissions(thisUserId);

            if (listPermissions != null)
            {
                List<Tuple<string, string>> permissions = new List<Tuple<string, string>>();
                foreach (Tuple<string, ShoppingListPermissionType> tuple in listPermissions)
                {
                    permissions.Add(Tuple.Create(tuple.Item1, tuple.Item2.ToString()));
                }
                return Ok(permissions);
            }
            else
            { 
                return BadRequest(new { message = "Not Found" });
            }
        }

        // Returns the permission that a user has to a list:
        // Attention: This method required the userId to be sent, not the user e-mail.
        //            This should be either changed to email or this method should be removed altogether.
        // \return string => ShoppingListPermissionType
        [Authorize(Roles = Role.User)]
        [HttpGet("listpermission/{listId}/{userId}")]
        public IActionResult GetUserListPermission(string listId, string userId)
        {
            string thisUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ShoppingListPermissionType permission = _shoppingService.GetUserListPermission(listId, thisUserId, userId);
            if (permission != ShoppingListPermissionType.Undefined)
                return Ok(permission.ToString());
            else
                return BadRequest(new { message = "Not Found" });
        }

        // Adds or if already existent updates the permission that target user has to target list
        // \param listPermission - Tuple<string, string, string> => TargetUserEMail, ShoppingListId, PermissionType
        [Authorize(Roles = Role.User)]
        [HttpPut("listpermission")]
        public async Task<IActionResult> AddOrUpdateListPermission([FromBody] object listPermission)
        {
            Tuple<string, string, string> tupel =
                JsonConvert.DeserializeObject<Tuple<string, string, string>>(listPermission.ToString());

            string thisUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string targetUserId = tupel.Item1;
            User user = _userService.GetById(targetUserId);
            if (user != null)
            {
                string shoppingListId = tupel.Item2;
                ShoppingListPermissionType permission =
                    (ShoppingListPermissionType)Enum.Parse(typeof(ShoppingListPermissionType), tupel.Item3, true);

                bool success = await _shoppingService.AddOrUpdateListPermission(thisUserId, targetUserId, shoppingListId, permission, true, true);
                if (success)
                    return Ok();
            }
            return BadRequest(new { message = "Permission Change not possible." });
        }

        [Authorize(Roles = Role.User)]
        [HttpDelete("listpermission/{listId}/{userId}")]
        public async Task<IActionResult> RemoveListPermission(string listId, string userId)
        {
            string thisUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool success = await _shoppingService.RemoveListPermission(thisUserId, userId, listId);
            if (success)
                return Ok();
            else
                return BadRequest(new { message = "Deleting of permission not possible." });
        }

        /// <summary>
        /// Generates a token of this lists share id. The share id can be used to share
        /// this list with other users, by them calling <see cref="AddListFromListShareId(string, string)"/>
        /// The share id is stored in <see cref="ShoppingList.ShareId"/>
        /// The token does not expire.
        /// </summary>
        /// <param name="listSyncId">Id of the list that's to be shared</param>
        /// <param name="currentUserId">Id of the currently logged in user</param>
        /// <returns>The share id.</returns>
        [HttpPost("generate_share_id/{listId}")]
        public IActionResult GenerateOrExtendListShareId(string listId)
        {
            var thisUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string listShareId = _shoppingService.GenerateOrExtendListShareId(listId, thisUserId);
            return Ok(listShareId);
        }

        /// <summary>
        /// Adds a <see cref="ShoppingListPermissionType.WriteAndModifyPermission"/> permission for the
        /// current user to the list with the given listShareId.
        /// 
        /// The list share id has to be generated with <see cref="GenerateOrExtendListShareId(string)"/>.
        /// 
        /// Possible status messages:
        /// <see cref="StatusMessages.ListNotFound">If there is no shopping list with the given sync id.</see>
        /// <see cref="StatusMessages.ListShareLinkExpired">If the link has been expired (older than 2 days).</see>
        /// <see cref="StatusMessages.ListAlreadyAdded">If the user that created the link doesn't exist.</see>
        /// </summary>
        /// <returns>The added lists share id.</returns>
        [HttpPost("list_share_id")]
        public async Task<IActionResult> AddListByShareId([FromBody] object jsonBody)
        {
            Tuple<string> listShareId = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ShoppingList listEntity = await _shoppingService.AddListFromListShareId(currentUserId, listShareId.Item1);
            return Ok(listEntity.SyncId);
        }

        [HttpGet("list_share_id/{shareId}")]
        public IActionResult GetListByShareId(string shareId)
        {
            ShoppingList listEntity = _shoppingService.GetListFromListShareId(shareId);
            return Ok(listEntity.SyncId);
        }

        /// <summary>
        /// This is the fallback when sharing a list via app-link.
        /// The link is generated on client side.
        /// </summary>
        /// <returns></returns>
        [HttpGet("lsid/{listId}")]
        [AllowAnonymous]
        public IActionResult HandleAppLinkAddContact(string listId)
        {
            string redirectToMainPage = HtmlPageFactory.CreateRedirectToShoppingNowPage();
            return base.Content(redirectToMainPage, "text/html");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="registrationToken">This registration token comes from the client FCM SDKs.</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost("test_firebase_push_message")]
        public async Task<IActionResult> TestFirebasePushMessage([FromBody] object jsonBody)
        {
            Tuple<string, string> tuple = JsonConvert.DeserializeObject<Tuple<string, string>>(jsonBody.ToString());
            string registrationToken = tuple.Item1;
            string body = tuple.Item2;
            // See documentation on defining a message payload.
            var message = new FirebaseAdmin.Messaging.Message()
            {
                Data = new Dictionary<string, string>()
                {
                    { "body", body },
                    { "title", "ServiceNow" },
                    { "sound", "default" }
                },
                Token = registrationToken,
            };

            // Send a message to the device corresponding to the provided
            // registration token.
            string response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
            // Response is a message ID string.
            Console.WriteLine("Successfully sent message: " + response);
            return Ok();
        }

    }

}
