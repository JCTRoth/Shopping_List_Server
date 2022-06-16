using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ShoppingListServer.Logic;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ShoppingListServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("rp")]
    public class ResetPasswordController : Controller
    {
        private IResetPasswordService _resetPasswordService;

        public ResetPasswordController(IResetPasswordService resetPasswordService)
        {
            _resetPasswordService = resetPasswordService;
        }

        /// <summary>
        /// First step of resetting the password is to request an email that contains the
        /// reset password code.
        /// </summary>
        /// <param name="jsonBody"></param>
        /// <returns></returns>
        [HttpPost("requestcode")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestResetPasswordCode([FromBody] object jsonBody)
        {
            Tuple<string> passwordUpdate = JsonConvert.DeserializeObject<Tuple<string>>(jsonBody.ToString());
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool success = await _resetPasswordService.SendResetPasswordEMailAndAddToken(currentUserId, passwordUpdate.Item1);
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
        [HttpPost("codevalid")]
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
    }
}
