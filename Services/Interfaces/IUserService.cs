using System.Collections.Generic;
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

        bool AddContact(string currentUserId, User targetUser, UserContactType type);

        void AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type);

        bool RemoveContact(string currentUserId, User targetUser);
        /// <summary>
        /// Generates and stores a new password for the given user given the cleartext password.
        /// The cleartext password is not stored.
        /// </summary>
        /// <param name="user">User for which a password is stored.</param>
        /// <param name="password">cleartext password</param>
        void HashUserPassword(User user, string password);

        List<UserContact> GetContacts(string userId);
    }
}
