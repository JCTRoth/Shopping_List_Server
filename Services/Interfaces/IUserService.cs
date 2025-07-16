using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IUserService
    {
        bool AddUser(User new_user, string password);

        Task<User> RegisterGoogleUser(GoogleUser googleUser, string accessToken, string password);
        Task<User> RegisterFacebookUser(FacebookProfile facebookProfile, string accessToken, string password);
        Task<User> RegisterAppleUser(AppleAccount appleAccount, string password);

        Result Authenticate(string id, string email, string password);
        User FindUser(User user, bool throwException = true);
        User FindUser(string id, string email, bool throwException = true);

        // Changing the email requires a new email verification.
        // To not change a field, just set it null.
        // Changes the following user properties:
        // - FirstName
        // - LastName
        // - UserName
        // - EMail
        // - Color
        bool UpdateUser(string currentUserId, User userUpdate);

        bool UpdateUserPassword(string currentUserId, string password);

        bool RemoveUser(string currentUserId);

        IEnumerable<User> GetAll();

        User GetById(string id);

        User GetByEMail(string email);

        /// <summary>
        /// Adds or updates targetUser as contact to the contacts list of current user.
        /// </summary>
        /// <param name="allowUpdate">Allows update of contact type if the contact already exists. If false, throws an exception in that case.</param>
        Task<bool> AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type, bool allowUpdate);

        bool RemoveContact(string currentUserId, User targetUser);
        /// <summary>
        /// Generates and stores a new password for the given user given the cleartext password.
        /// The cleartext password is not stored.
        /// </summary>
        /// <param name="user">User for which a password is stored.</param>
        /// <param name="password">cleartext password</param>
        void AddUserPassword(User user, string password);

        /// <summary>
        /// Generates a token of the new user share id. The user share id can be used by someone
        /// else to add themselfs to the users contact list and vice versa,
        /// see <see cref="AddUserFromContactShareId(string, string)"/>
        /// Writes it in <see cref="User.ContactShareId"/>
        /// The token expires after two days.
        /// </summary>
        /// <param name="currentUserId">Id of the currently logged in user</param>
        /// <returns>The share id.</returns>
        string GenerateOrExtendContactShareId(string currentUserId);

        /// <summary>
        /// Add the currently logged in user to the contacts of the user that created the given contactSharedId.
        /// (with <see cref="GenerateOrExtendContactShareId(string)"/>)
        /// 
        /// Possible status messages:
        /// <see cref="StatusMessages.CannotAddYourselfAsContact"/> if the link was created by the logged in user.
        /// <see cref="StatusMessages.ContactLinkExpired"/> if the link has been expired (older than 2 days).
        /// <see cref="StatusMessages.UserNotFound"/> if the user that created the link doesn't exist.
        /// </summary>
        /// <param name="currentUserId"></param>
        /// <param name="contactShareId"><see cref="User.ContactShareId"/> created by another user.</param>
        /// <returns>Target user</returns>
        Task<User> AddUserFromContactShareId(string currentUserId, string contactShareId);
        User GetUserFromContactShareId(string contactShareId);

        List<UserContact> GetContacts(string userId);

        /// <summary>
        /// Assigns the given FcmToken to the given user. FcmTokens are used to send push notifications
        /// (notifications that reach the client if the app is in background or closed)
        /// to users via Firebase Cloud Messaging.
        /// If a user has no token assigned, they are unable to receive push notifications.
        /// </summary>
        /// <param name="fcmToken"></param>
        void RegisterFcmToken(string currentUserId, string fcmToken);

        /// <summary>
        /// Unregisters the FcmToken assigned to this user.
        /// </summary>
        /// <param name="currentUserId"></param>
        /// <param name="fcmToken"></param>
        void UnregisterFcmToken(string currentUserId, string fcmToken);

        Task<bool> AddOrUpdateProfilePicture(string currentUserId, IFormFile picture, ImageInfo info);

        void UpdateProfilePictureTransformation(string currentUserId, ImageTransformationDTO transformation);

        bool RemoveProfilePicture(string currentUserId);

        ImageInfo GetProfilePictureInfo(string userId);

        Task<byte[]> GetProfilePicture(string userId);

        List<UserPictureLastChangeTimeDTO> GetUserPictureLastChangeTimes(string currentUserId);
    }
}
