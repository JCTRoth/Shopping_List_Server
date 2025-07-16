using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShoppingListServer.Entities;
using Newtonsoft.Json;
using ShoppingListServer.Models;
using ShoppingListServer.Helpers;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Services.Interfaces;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using ShoppingListServer.Logic;
using Microsoft.AspNetCore.Http;


namespace ShoppingListServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        protected IUserService _userService;
        protected IShoppingService _shoppingService;
        protected IEMailVerificationService _emailVerificationService;
        protected IAuthenticationService _authenticationService;

        public UsersController(
            IUserService userService,
            IShoppingService shoppingService,
            IEMailVerificationService emailVerificationService,
            IAuthenticationService authenticationService)
        {
            _userService = userService;
            _shoppingService = shoppingService;
            _emailVerificationService = emailVerificationService;
            _authenticationService = authenticationService;
        }

        // Is used to check if the server is reachable.
        [AllowAnonymous]
        [HttpHead("test")]
        public ActionResult TestMe()
        {
            return Ok();
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody]Authenticate model)
        {
            Result user = _userService.Authenticate(model.Id, model.Email, model.Password);

            if (user.WasFound == false)
                return BadRequest(new { message = "Id or password is incorrect" });

            return Ok(user.ReturnValue);
        }

        // Used to register users with id only or full users
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] object user_json_object)
        {
            RegisterRequest registerRequest = JsonConvert.DeserializeObject<RegisterRequest>(user_json_object.ToString());

            User new_user = new User
            {
                EMail = registerRequest.EMail,
                FirstName = registerRequest.FirstName,
                LastName = registerRequest.LastName,
                Username = registerRequest.Username
            };

            bool success = _userService.AddUser(new_user, registerRequest.Password);
            if (success)
            {
                await _emailVerificationService.SendEMailVerificationCodeAndAddToken(new_user.Id);
                return Ok(new_user);
            }
            else
            {
                return BadRequest(new { message = "Not registered" });
            };

        }

        [AllowAnonymous]
        [HttpPost("register_apple")]
        public async Task<IActionResult> RegisterApple([FromBody] object json)
        {
            Tuple<AppleAccount, string> tuple = JsonConvert.DeserializeObject<Tuple<AppleAccount, string>>(json.ToString());
            AppleAccount appleAccount = tuple.Item1;
            string password = tuple.Item2;
            User user = await _userService.RegisterAppleUser(appleAccount, password);
            if (user != null)
            {
                return Ok(user.WithoutPassword());
            }
            return BadRequest(new { message = StatusMessages.SomethingWentWrong });
        }

        [AllowAnonymous]
        [HttpPost("register_google")]
        public async Task<IActionResult> RegisterGoogle([FromBody] object json)
        {
            Tuple<GoogleUser, string, string> tuple = JsonConvert.DeserializeObject<Tuple<GoogleUser, string, string>>(json.ToString());
            GoogleUser googleUser = tuple.Item1;
            string accessToken = tuple.Item2;
            string password = tuple.Item3;

            User user = await _userService.RegisterGoogleUser(googleUser, accessToken, password);
            if (user != null)
            {
                return Ok(user.WithoutPassword());
            }
            return BadRequest(new { message = StatusMessages.SomethingWentWrong });
        }

        [AllowAnonymous]
        [HttpPost("register_facebook")]
        public async Task<IActionResult> RegisterFacebook([FromBody] object json)
        {
            Tuple<FacebookProfile, string, string> tuple = JsonConvert.DeserializeObject<Tuple<FacebookProfile, string, string>>(json.ToString());
            FacebookProfile facebookProfile = tuple.Item1;
            string accessToken = tuple.Item2;
            string password = tuple.Item3;
            // verify access token whith this link:
            // https://graph.facebook.com/debug_token?input_token=EAAHOgab9jbkBAOZAmqu8ekE0y1VZBGFbfsWHBZCOZAW8fo6mAWIkR8uJmAgo4LXn69ImOeURBhZAzYDT4bYyuNqQegOkF3wA2k1JXZBbGghD3ZBDXrt3sqdyDRx6UjeJBa5XuT1qYiSHJXjYZA4oLLUdZBObGwWz3lHEEg9caUxItzZBcZAy52fCCjhid0pR3rDUOutHtp0Ppi0QgkJO9EYni2Ff0pyJwgpqtxAGuYTwWRFDj2YKZAAChP8BEjKXyfZBwKZC0ZD&access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w
            // https://graph.facebook.com/debug_token?input_token=<user-token>&access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w
            // verify that user exists
            // https://graph.facebook.com/me?access_token=EAAHOgab9jbkBAOZAmqu8ekE0y1VZBGFbfsWHBZCOZAW8fo6mAWIkR8uJmAgo4LXn69ImOeURBhZAzYDT4bYyuNqQegOkF3wA2k1JXZBbGghD3ZBDXrt3sqdyDRx6UjeJBa5XuT1qYiSHJXjYZA4oLLUdZBObGwWz3lHEEg9caUxItzZBcZAy52fCCjhid0pR3rDUOutHtp0Ppi0QgkJO9EYni2Ff0pyJwgpqtxAGuYTwWRFDj2YKZAAChP8BEjKXyfZBwKZC0ZD

            // access_token=508531224448441
            // graph.facebook.com/debug_token?input_token={token-to-inspect}&access_token={app-token-or-admin-token}
            // https://graph.facebook.com/113716804852420?access_token=508531224448441|c_eL2lzG70c4N-6griSVFv2-f5w

            User user = await _userService.RegisterFacebookUser(facebookProfile, accessToken, password);
            if (user != null)
            {
                return Ok(user.WithoutPassword());
            }
            return BadRequest(new { message = StatusMessages.SomethingWentWrong });
        }

        /*
        [Authorize(Roles = Role.User)]
        [HttpGet("all")]
        public IActionResult GetAll()
        {
            var users =  _userService.GetAll();
            return Ok(users);
        }
        */

        // Updates a user given a User serialized json object. Only fields
        // that are not null are updated. It't possible to update the following
        // properties with this method:
        // - FirstName
        // - LastName
        // - UserName
        // - EMail
        // - Color
        //
        // Update the password with a PATCH request to /users/password.
        [HttpPatch("user")]
        public IActionResult UpdateUser([FromBody] object jsonBody)
        {
            User userUpdate = JsonConvert.DeserializeObject<User>(jsonBody.ToString());
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool success = _userService.UpdateUser(currentUserId, userUpdate);
            if (success)
                return Ok();
            else
                return BadRequest(StatusMessages.SomethingWentWrong);
        }

        /// <summary>
        /// Delets the logged in users account.
        /// Removes all contacts of the user and other user that have this user as contact.
        /// Removes all list permissions. Does not remove lists for everyone for those lists where this user has delete permissions on.
        /// </summary>
        /// <returns></returns>
        [HttpDelete("user")]
        public async Task<IActionResult> RemoveUser()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Delete all lists or permissions of the user to the list.
            await _shoppingService.DeleteAllListsOfUser(currentUserId, false);
            // Remove the user from the database.
            bool success = _userService.RemoveUser(currentUserId);
            if (success)
                return Ok();
            else
                return BadRequest(StatusMessages.SomethingWentWrong);
        }

        // Update the currently logged ins password. The password will not
        // be stored in clear text but instead in a hashed form using a
        // secure SHA​-512 hashing operation with a random salt.
        // Warning: Currently, it is possible to change the password without providing the current password.
        //          This would allow someone else that has access to the device to change the password without the owner noticing.
        // \jsonBody - serialized json string object that is the password in clear text.
        [HttpPatch("password")]
        public IActionResult UpdatePassword([FromBody] object jsonBody)
        {
            string passwordUpdate = JsonConvert.DeserializeObject<string>(jsonBody.ToString());
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool success = _userService.UpdateUserPassword(currentUserId, passwordUpdate);
            if (success)
                return Ok();
            else
                return BadRequest(StatusMessages.SomethingWentWrong);
        }

        /// <summary>
        /// This is the fallback when the link in the reset password e-mail is clicked
        /// although the app isn't installed. This link should only be used as app-link.
        /// see <see cref="IResetPasswordService.SendResetPasswordEMailAndAddToken(string)"/>
        /// </summary>
        /// <returns></returns>
        [HttpGet("rp/{code}")]
        [AllowAnonymous]
        public IActionResult HandleAppLinkResetPassword(string code)
        {
            string redirectToMainPage = HtmlPageFactory.CreateRedirectToShoppingNowPage();
            return base.Content(redirectToMainPage, "text/html");
        }

        [HttpGet("{id}")]
        public IActionResult GetById(string id)
        {
            // only allow admin to access other user records
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (id != currentUserId && !User.IsInRole(Role.User))
                return Forbid();

            var user = _userService.GetById(id);

            if (user == null)
                return NotFound();

            return Ok(user.WithoutPassword());
        }

        // Redirects the user to the main page.
        //
        // This link should be catched by the ShoppingNow app before this post request is even sent.
        // The app will then send a /contact/{userId} post instead which will be processed in AddContact().
        // If ShoppingNow isn't installed, the user is instead redirected to the main page where
        // they can find a link to install the app.
        [HttpPost("contactlink/{userId}")]
        [AllowAnonymous]
        public IActionResult AddContactViaLink(string userId)
        {
            string redirectToMainPage = HtmlPageFactory.CreateRedirectToShoppingNowPage();
            return base.Content(redirectToMainPage, "text/html");
        }

        [HttpPost("contact")]
        public async Task<IActionResult> AddContact([FromBody] object jsonBody)
        {
            UserContactDTO contact = JsonConvert.DeserializeObject<UserContactDTO>(jsonBody.ToString());
            await _userService.AddOrUpdateContact(User.FindFirstValue(ClaimTypes.NameIdentifier), contact.User, contact.Type, false);
            return Ok();
        }

        [HttpPatch("contact")]
        public async Task<IActionResult> UpdateContact([FromBody] object jsonBody)
        {
            UserContactDTO contact = JsonConvert.DeserializeObject<UserContactDTO>(jsonBody.ToString());
            await _userService.AddOrUpdateContact(User.FindFirstValue(ClaimTypes.NameIdentifier), contact.User, contact.Type, true);
            return Ok();
        }

        [HttpDelete("contact/{userId}")]
        public IActionResult RemoveContact(string userId)
        {
            bool success = _userService.RemoveContact(User.FindFirstValue(ClaimTypes.NameIdentifier), new User() { Id = userId });
            if (success)
                return Ok();
            else
                return BadRequest(StatusMessages.ContactNotFound);
        }

        [HttpGet("contacts")]
        public IActionResult GetContacts()
        {
            List<UserContact> contacts = _userService.GetContacts(User.FindFirstValue(ClaimTypes.NameIdentifier));
            List<UserContactDTO> contactsReturn = new List<UserContactDTO>();
            foreach (UserContact contact in contacts)
            {
                contactsReturn.Add(new UserContactDTO(contact.UserTarget.WithoutPassword(), contact.UserContactType));
            }
            if (contactsReturn != null)
                return Ok(contactsReturn);
            else
                return BadRequest(StatusMessages.SomethingWentWrong);
        }

        /// <summary>
        /// Generates a token of the new user share id. The user share id can be used by someone
        /// else to add themselfs to the users contact list and vice versa,
        /// see <see cref="AddUserFromContactShareId(string, string)"/>
        /// Writes it in <see cref="User.ContactShareId"/>
        /// The token expires after two days.
        /// </summary>
        /// <param name="currentUserId">Id of the currently logged in user</param>
        /// <returns>The share id.</returns>
        [HttpPost("generate_share_id")]
        public IActionResult GenerateOrExtendContactShareId()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string contactShareId = _userService.GenerateOrExtendContactShareId(currentUserId);
            return Ok(contactShareId);
        }

        /// <summary>
        /// Add the currently logged in user to contacts of the user that created the given contactSharedId.
        /// (with <see cref="GenerateOrExtendContactShareId(string)"/>)
        /// 
        /// Possible status messages:
        /// <see cref="StatusMessages.CannotAddYourselfAsContact"/> if the link was created by the logged in user.
        /// <see cref="StatusMessages.ContactLinkExpired"/> if the link has been expired (older than 2 days).
        /// <see cref="StatusMessages.UserNotFound"/> if the user that created the link doesn't exist.
        /// </summary>
        /// <param name="currentUserId"></param>
        /// <param name="contactShareId"><see cref="User.ContactShareId"/> created by another user.</param>
        [HttpPost("contact_share_id")]
        public async Task<IActionResult> AddContactByShareId([FromBody] object jsonBody)
        {
            Tuple<string> contactShareId = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User targetUser = await _userService.AddUserFromContactShareId(currentUserId, contactShareId.Item1);
            return Ok(targetUser.WithoutPassword());
        }

        [HttpGet("contact_share_id/{contactShareId}")]
        public IActionResult GetUserByShareId(string contactShareId)
        {
            User targetUser = _userService.GetUserFromContactShareId(contactShareId);
            return Ok(targetUser.WithoutPassword());
        }

        /// <summary>
        /// This is the fallback when adding a new contact via app-link.
        /// The link is generated on client side.
        /// </summary>
        /// <returns></returns>
        [HttpGet("csid/{contactId}")]
        [AllowAnonymous]
        public IActionResult HandleAppLinkAddContact(string contactId)
        {
            string redirectToMainPage = HtmlPageFactory.CreateRedirectToShoppingNowPage();
            return base.Content(redirectToMainPage, "text/html");
        }

        /// <summary>
        /// Assigns the given FcmToken to the given user. Every user can only have one FcmToken assigned
        /// meaning that only one device can receive push notifications per user.
        /// FcmTokens are used to send push notifications
        /// (notifications that reach the client if the app is in background or closed)
        /// to users via Firebase Cloud Messaging.
        /// If a user has no token assigned, they are unable to receive push notifications.
        /// </summary>
        /// <param name="jsonBody">Tuple<string> containing the FcmToken</param>
        /// <exception cref="UserNotFoundException">If the user could not be found.</exception>
        [HttpPost("register_fcm_token")]
        public IActionResult RegisterFcmToken([FromBody] object jsonBody)
        {
            Tuple<string> fcmToken = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userService.RegisterFcmToken(currentUserId, fcmToken.Item1);
            return Ok();
        }

        [HttpPost("unregister_fcm_token")]
        public IActionResult UnregisterFcmToken([FromBody] object jsonBody)
        {
            Tuple<string> fcmToken = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userService.UnregisterFcmToken(currentUserId, fcmToken.Item1);
            return Ok();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file">jpg profile picture</param>
        /// <param name="jsonString">ImageTransformationDTO</param>
        /// <returns></returns>
        [HttpPost("profile_picture")]
        public async Task<IActionResult> AddOrUpdateProfilePicture([FromForm] IFormFile file, [FromForm] string jsonString)
        {
            ImageInfo info = JsonConvert.DeserializeObject<ImageInfo>(jsonString.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _userService.AddOrUpdateProfilePicture(currentUserId, file, info);
            return Ok();
        }

        /// <summary>
        /// To update only the transformation of the picture and not the image file itself.
        /// </summary>
        /// <param name="jsonBody">ImageTransformationDTO</param>
        /// <returns></returns>
        [HttpPost("profile_picture_transformation")]
        public IActionResult UpdateProfilePictureTransformation([FromBody] object jsonBody)
        {
            ImageTransformationDTO transformation = JsonConvert.DeserializeObject<ImageTransformationDTO>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userService.UpdateProfilePictureTransformation(currentUserId, transformation);
            return Ok();
        }

        [HttpDelete("profile_picture")]
        public IActionResult RemoveProfilePicture()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userService.RemoveProfilePicture(currentUserId);
            return Ok();
        }

        /// <summary>
        /// Returns last change times of all the profile pictures of users that this user can be associated with.
        /// Associated users are those that are in the contacts list and that appear in shared lists.
        /// </summary>
        /// <returns>List<UserPictureLastChangeTimeDTO></returns>
        [Authorize(Roles = Role.User)]
        [HttpGet("profile_picture_lastchange")]
        public IActionResult GetProfilePictureLastChangeTimes()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<UserPictureLastChangeTimeDTO> lastChangeTimes = _userService.GetUserPictureLastChangeTimes(currentUserId);
            if (lastChangeTimes != null)
            {
                return Ok(lastChangeTimes);
            }
            return BadRequest(new { message = "Not Found" });
        }

        /// <summary>
        /// Returns <see cref="User.ProfilePictureInfo"/> of the profile picture of the given user.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>ImageInfo</returns>
        [Authorize(Roles = Role.User)]
        [HttpGet("profile_picture_info/{userId}")]
        public IActionResult GetProfilePictureInfo(string userId)
        {
            return Ok(_userService.GetProfilePictureInfo(userId));
        }

        /// <summary>
        /// Returns the profile picture of target user.
        /// </summary>
        /// <param name="userId">target user</param>
        /// <returns>byte[]</returns>
        [Authorize(Roles = Role.User)]
        [HttpGet("profile_picture/{userId}")]
        public async Task<IActionResult> GetProfilePicture(string userId)
        {
            byte[] bytes = await _userService.GetProfilePicture(userId);
            return File(bytes, "image/jpeg");
        }

    }
}
