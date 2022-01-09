using Microsoft.Extensions.Options;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.Models;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ShoppingListServer.Services
{
    // Provides methods for verifying e-mail addresses of users. If usually used when
    // a user registers but can also be used to register a new e-mail address for a user
    // or reverify its existing one.
    // A user is verified when user.IsVerified == True
    // Users for which this flag is already set are ignored.
    public class EMailVerificationService : IEMailVerificationService
    {
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;

        public EMailVerificationService(IOptions<AppSettings> appSettings, AppDb db)
        {
            _appSettings = appSettings.Value;
            _db = db;
        }

        public async Task<bool> SendEMailVerificationCodeAndAddToken(User user)
        {
            if (string.IsNullOrEmpty(user.EMail))
                return false;
            EMailVerificationToken token = AddEMailVerificationToken(user);
            return await SendEMailWithUrlCode(user.EMail, token.UrlCode);
        }

        // Sends a verification e-mail 
        private async Task<bool> SendEMailWithUrlCode(string targetEmail, string urlCode)
        {
            SmtpClient client = new SmtpClient();
            client.Host = _appSettings.NoReplyEMailHost;
            client.Port = _appSettings.NoReplyEMailPort;
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_appSettings.NoReplyEMailAddress, _appSettings.NoReplyEMailPassword);

            MailMessage message = new MailMessage("noreply@shopping-now.net", targetEmail);
            message.Body = "Thank you for registering for ShoppingNow.\n" +
                "If you haven't tried to register, you can ignore this mail.\n\n" +
                "Please click the following link to complete registration:\n" +
                "https://shopping-now.net:5678/users/verify/" + urlCode;
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.Subject = "ShoppingNow Registration";
            message.SubjectEncoding = System.Text.Encoding.UTF8;

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while sending a mail to " + targetEmail + ": " + e.Message);
                return false;
            }
            return true;
        }

        // Adds a verification token for the given user that expires in 30 days.
        private EMailVerificationToken AddEMailVerificationToken(User user)
        {
            EMailVerificationToken token = new EMailVerificationToken(
                Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(30));
            user.EMailVerificationTokens.Add(token);
            token.UserId = user.Id;
            token.User = user;
            _db.SaveChanges();
            return token;
        }

        private void RemoveEMailVerificationToken(User user, EMailVerificationToken token)
        {
            user.EMailVerificationTokens.Remove(token);
            _db.SaveChanges();
        }

        public User VerifyEMailUrlCode(string urlCode)
        {
            var tokenQuery = from token in _db.Set<EMailVerificationToken>()
                        where token.UrlCode.Equals(urlCode)
                        select token;
            EMailVerificationToken t = tokenQuery.FirstOrDefault();
            User u = null;
            if (t != null)
            {
                if (DateTime.UtcNow > t.ExpirationTime)
                    throw new Exception("Link Expired");
                var userQuery = from token in _db.Set<EMailVerificationToken>()
                                join user in _db.Set<User>()
                                on token.UserId equals user.Id
                                select user;
                u = userQuery.FirstOrDefault();
                if (u != null)
                {
                    u.EMailVerificationTokens.Clear();
                    if (!u.IsVerified)
                    {
                        u.IsVerified = true;
                    }
                }
            }
            _db.SaveChanges();
            return u;
        }
    }
}
