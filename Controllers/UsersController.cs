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

namespace ShoppingListServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        protected IUserService _userService;
        protected IEMailVerificationService _emailVerificationService;
        protected IResetPasswordService _resetPasswordService;

        public UsersController(
            IUserService userService,
            IEMailVerificationService emailVerificationService,
            IResetPasswordService resetPasswordService)
        {
            _userService = userService;
            _emailVerificationService = emailVerificationService;
            _resetPasswordService = resetPasswordService;
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
                Id = Guid.NewGuid().ToString(),
                EMail = registerRequest.EMail,
                FirstName = registerRequest.FirstName,
                LastName = registerRequest.LastName,
                Username = registerRequest.Username
            };

            if (_userService.AddUser(new_user, registerRequest.Password))
            {
                await _emailVerificationService.SendEMailVerificationCodeAndAddToken(new_user);
                return Ok(new_user);
            }
            else
            {
                return BadRequest(new { message = "Not registered" });
            };

        }

        // This address is supposed to be called from a web browser.
        // Verify a user given a code that was sent to their email address when they registered.
        // Returns a simple html page with a message of what happened.
        [AllowAnonymous]
        [HttpGet("verify/{urlCode}")]
        public ContentResult Verify(string urlCode)
        {
            try
            {
                User user = _emailVerificationService.VerifyEMailUrlCode(urlCode);
                // Return simple html page.
                if (user != null)
                {
                    return base.Content("<div>Successfully registered!</div>", "text/html");
                }
                else
                {
                    return base.Content("<div>Oops, something went wrong :(</div>", "text/html");
                }
            }
            catch (Exception e)
            {
                return base.Content("<div>Something went wrong: " + e.Message + "</div>", "text/html");
            }
        }

        /// <summary>
        /// Requests the server to resend the verification mail that contains
        /// a link to verify your entered E-Mail address.
        /// 
        /// Possible StatusMessages:
        /// <see cref="StatusMessages.UserIsAlreadyVerified"/>
        /// <see cref="StatusMessages.UserHasNoEMailAddress"/>
        /// </summary>
        [HttpPost("resendVerificationEMail")]
        public async Task<IActionResult> ResendVerificationEMail()
        {
            User user = _userService.GetById(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (user != null)
            {
                if (await _emailVerificationService.SendEMailVerificationCodeAndAddToken(user))
                {
                    return Ok();
                }
            }
            return BadRequest(new { message = "User does not exist." });
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
                return BadRequest("Something went wrong :(");
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
                return BadRequest("Something went wrong :(");
        }

        /// <summary>
        /// First step of resetting the password is to request an email that contains the
        /// reset password code.
        /// </summary>
        /// <param name="jsonBody"></param>
        /// <returns></returns>
        [HttpPost("resetpassword_requestcode")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestResetPasswordCode([FromBody] object jsonBody)
        {
            Tuple<string> passwordUpdate = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            bool success = await _resetPasswordService.SendResetPasswordEMailAndAddToken(passwordUpdate.Item1);
            if (success)
                return Ok();
            else
                return BadRequest("Something went wrong.");
        }

        /// <summary>
        /// Second step of resetting the password is to check if the entered
        /// reset password code is valid. It should be the same as the one
        /// that was sent by mail with
        /// <see cref="RequestResetPasswordCode(object)"/>
        /// </summary>
        /// <param name="jsonBody"></param>
        /// <returns></returns>
        [HttpPost("resetpassword_codevalid")]
        [AllowAnonymous]
        public IActionResult IsResetPasswordCodeValid([FromBody] object jsonBody)
        {
            Tuple<string, string> tuple = JsonConvert.DeserializeObject<Tuple<string, string>>(jsonBody.ToString());
            string email = tuple.Item1;
            string code = tuple.Item2;
            bool success = _resetPasswordService.CheckIfCodeExistsWithException(email, code);
            if (success)
                return Ok();
            else
                return BadRequest("Something went wrong.");
        }

        /// <summary>
        /// Thirds step of resetting the password is to actually reset it using
        /// the code that was sent via email, see
        /// <see cref="RequestResetPasswordCode(object)"/>
        /// </summary>
        /// <param name="jsonBody"></param>
        /// <returns></returns>
        [HttpPost("resetpassword")]
        [AllowAnonymous]
        public IActionResult ResetPassword([FromBody] object jsonBody)
        {
            Tuple<string, string, string> tuple = JsonConvert.DeserializeObject<Tuple<string, string, string>>(jsonBody.ToString());
            string email = tuple.Item1;
            string code = tuple.Item2;
            string newPassword = tuple.Item3;
            bool success = _resetPasswordService.SetPassword(email, code, newPassword);
            if (success)
                return Ok();
            else
                return BadRequest("Something went wrong.");
        }

        /// <summary>
        /// This method is called when a user clicks on the link of the "Reset Password" email
        /// but does not have ShoppingNow installed. In that case, show a message, that the
        /// app is not installed and provide a link to the app stores. (TODO)
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [HttpPost("resetpassword2")]
        [AllowAnonymous]
        public IActionResult ResetPasswordLink(string code)
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
        public IActionResult AddContact([FromBody] object jsonBody)
        {
            UserContactDTO contact = JsonConvert.DeserializeObject<UserContactDTO>(jsonBody.ToString());
            _userService.AddOrUpdateContact(User.FindFirstValue(ClaimTypes.NameIdentifier), contact.User, contact.Type, false);
            return Ok();
        }

        [HttpPatch("contact")]
        public IActionResult UpdateContact([FromBody] object jsonBody)
        {
            UserContactDTO contact = JsonConvert.DeserializeObject<UserContactDTO>(jsonBody.ToString());
            _userService.AddOrUpdateContact(User.FindFirstValue(ClaimTypes.NameIdentifier), contact.User, contact.Type, true);
            return Ok();
        }

        [HttpDelete("contact/{userId}")]
        public IActionResult RemoveContact(string userId)
        {
            bool success = _userService.RemoveContact(User.FindFirstValue(ClaimTypes.NameIdentifier), new User() { Id = userId });
            if (success)
                return Ok();
            else
                return BadRequest("There was no contact to remove.");
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
                return BadRequest("Something went wrong.");
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
        public IActionResult AddContactByShareId([FromBody] object jsonBody)
        {
            Tuple<string> contactShareId = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userService.AddUserFromContactShareId(currentUserId, contactShareId.Item1);
            return Ok();
        }

    }
}
