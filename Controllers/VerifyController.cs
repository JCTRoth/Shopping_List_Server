using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoppingListServer.Entities;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShoppingListServer.Controllers

{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class VerifyController : Controller
    {
        private IEMailVerificationService _emailVerificationService;

        public VerifyController(IEMailVerificationService emailVerificationService)
        {
            _emailVerificationService = emailVerificationService;
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
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (await _emailVerificationService.SendEMailVerificationCodeAndAddToken(userId))
            {
                return Ok();
            }
            return BadRequest(new { message = "Something went wrong." });
        }

        // This address is supposed to be called from a web browser.
        // Verify a user given a code that was sent to their email address when they registered.
        // Returns a simple html page with a message of what happened.
        [AllowAnonymous]
        [HttpGet("em/{urlCode}")]
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
    }
}
