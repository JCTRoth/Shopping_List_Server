using System.Collections.Generic;
using System.Threading.Tasks;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IUserService
    {
        bool AddUser(User new_user, string password);
        Result Authenticate(string id, string email, string password);
        User FindUser(User user);
        User FindUser(string id, string email);

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

        IEnumerable<User> GetAll();

        User GetById(string id);

        User GetByEMail(string email);

        /// <summary>
        /// Adds or updates targetUser as contact to the contacts list of current user.
        /// </summary>
        /// <param name="allowUpdate">Allows update of contact type if the contact already exists. If false, throws an exception in that case.</param>
        Task AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type, bool allowUpdate);

        bool RemoveContact(string currentUserId, User targetUser);
        /// <summary>
        /// Generates and stores a new password for the given user given the cleartext password.
        /// The cleartext password is not stored.
        /// </summary>
        /// <param name="user">User for which a password is stored.</param>
        /// <param name="password">cleartext password</param>
        void HashUserPassword(User user, string password);

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

        List<UserContact> GetContacts(string userId);
    }
}
