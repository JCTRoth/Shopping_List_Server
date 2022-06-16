using Microsoft.Extensions.Options;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Exceptions;
using ShoppingListServer.Helpers;
using ShoppingListServer.Logic;
using ShoppingListServer.Models;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ShoppingListServer.Services
{
    public class ResetPasswordService : IResetPasswordService
    {
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;
        private readonly IUserService _userService;
        private readonly IUserHub _userHub;

        public ResetPasswordService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserService userService,
            IUserHub userHub)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _userService = userService;
            _userHub = userHub;
        }

        public async Task<bool> SendResetPasswordEMailAndAddToken(string userId, string email)
        {
            ResetPasswordToken token = AddResetPasswordToken(email);
            User user = _db.FindUser_ID(userId);
            return await SendResetPasswordEMail(user.Username, email, token.Code);
        }

        /// <summary>
        /// Sends a mail with the reset password code.
        /// </summary>
        /// <param name="targetEMail"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        private async Task<bool> SendResetPasswordEMail(string username, string targetEMail, string code)
        {
            SmtpClient client = new SmtpClient();
            client.Host = _appSettings.NoReplyEMailHost;
            client.Port = _appSettings.NoReplyEMailPort;
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_appSettings.NoReplyEMailAddress, _appSettings.NoReplyEMailPassword);

            string link = "https://shopping-now.net/users/rp/" + code;
            string htmlBody =
                HtmlPageFactory.CreateHtmlHeader() +
                HtmlPageFactory.CreateGreeting(username) +
                "You can reset your password by entering the following code in your app:<br><br>\n" +
                "Code:\n" +
                "<h2>" + code + "</h2>\n" +
                "If you have ShoppingNow installed, you can just click the following button:<br><br>\n" +
                HtmlPageFactory.CreateButton("Reset Password", link) + "<br>\n" +
                "or click here:<br>\n" +
                HtmlPageFactory.CreateHyperlink(link, link) + "<br><br>\n" +
                HtmlPageFactory.CreateNoReplyDisclaimer() +
                HtmlPageFactory.CreateHtmlFooter();
            MailMessage message = HtmlPageFactory.CreateMailMessageTemplate(targetEMail, htmlBody);

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while sending a mail to " + targetEMail + ": " + e.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Adds a reset password token for the given user that expires in 30 days.
        /// </summary>
        /// <exception cref="UserNotFoundException">If the user could not be found.</exception>
        private ResetPasswordToken AddResetPasswordToken(string email)
        {
            User user = _userService.FindUser(null, email);
            int rndNumber = RandomNumberGenerator.GetInt32(0, 99999);
            string code = string.Format("{0:00000}", rndNumber);
            ResetPasswordToken token = new ResetPasswordToken(
                user, code, DateTime.UtcNow.AddDays(30));
            user.ResetPasswordTokens.Add(token);
            token.UserId = user.Id;
            token.User = user;
            _db.SaveChanges();
            return token;
        }

        public bool CheckIfCodeExistsWithException(string email, string code)
        {
            if (!CheckIfCodeExists(email, code))
                throw new ResetPasswordCodeMissingException(email, code);
            var tokenQuery = from token in _db.Set<ResetPasswordToken>()
                             where token.User.EMail.Equals(email) &&
                             token.Code.Equals(code)
                             select token;
            return tokenQuery.Any();
        }

        /// <summary>
        /// Verifies that the given code exists for the given email.
        /// 
        /// Use this method to check if the code the user enterd is correct
        /// before preceeding to a dialog where they are prompted to enter a new password.
        /// In that dialog SetPassword should be called.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        private bool CheckIfCodeExists(string email, string code)
        {
            var tokenQuery = from token in _db.Set<ResetPasswordToken>()
                             where token.User.EMail.Equals(email) &&
                             token.Code.Equals(code)
                             select token;
            return tokenQuery.Any();
        }

        public bool SetPassword(string email, string code, string newPassword)
        {
            User user = _userService.FindUser(null, email);
            CheckIfCodeExistsWithException(email, code);
            ResetPasswordToken token = user.ResetPasswordTokens.FirstOrDefault(token => token.Code == code);

            bool success = token != null;
            if (success)
            {
                if (DateTime.UtcNow > token.ExpirationTime)
                    throw new Exception(StatusMessages.PasswordResetCodeExpired);

                _userService.HashUserPassword(token.User, newPassword);
                token.User.ResetPasswordTokens.Clear();
                _db.SaveChanges();
            }
            return success;
        }
    }
}
