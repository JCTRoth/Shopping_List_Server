using System;
namespace ShoppingListServer.Models
{
    public class AppleAccount
    {
        /// <summary>
        /// Can be real or fake e-mail adress. Not necessarily unique
        /// for the same user on different devices.
        /// </summary>
        public string Email { get; set; }
        public string Name { get; set; }
        public string IdentityToken { get; set; }
        public string AuthorizationCode { get; set; }
        public string RealUserStatus { get; set; }

        /// <summary>
        /// Unique for the same user on different devices for this application.
        /// </summary>
        public string UserId { get; set; }

        public AppleAccount()
        {
        }
    }
}
