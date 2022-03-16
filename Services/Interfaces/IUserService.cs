using System.Collections.Generic;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IUserService
    {
        Result Authenticate(string id, string email, string password);

        bool AddUser(User new_user, string password);

        IEnumerable<User> GetAll();

        User GetById(string id);

        User GetByEMail(string email);

        bool AddContact(string currentUserId, User targetUser, UserContactType type);

        void AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type);

        bool RemoveContact(string currentUserId, User targetUser);

        List<UserContact> GetContacts(string userId);
    }
}
