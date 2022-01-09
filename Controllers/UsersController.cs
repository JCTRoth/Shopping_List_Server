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

namespace ShoppingListServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        protected IUserService _userService;
        protected IEMailVerificationService _emailVerificationService;

        public UsersController(IUserService userService, IEMailVerificationService emailVerificationService)
        {
            _userService = userService;
            _emailVerificationService = emailVerificationService;
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


        [HttpGet("{id}")]
        public IActionResult GetById(string id)
        {
            // only allow admin to access other user records
            var currentUserId = User.Identity.Name;

            if (id != currentUserId && !User.IsInRole(Role.User))
                return Forbid();

            var user =  _userService.GetById(id);

            if (user == null)
                return NotFound();

            return Ok(user.WithoutPassword());
        }
    }
}
