using ShoppingListServer.Entities;
using ShoppingListServer.Exceptions;
using System.Linq;

namespace ShoppingListServer.Database
{
    public static class AppDbExtensions
    {
        /// <summary>
        /// Returns null when user not found
        /// Users without password set will have pw null
        /// Requests without password field will have value null
        /// </summary>
        public static User FindUser_ID(this AppDb db, string id)
        {
            var user = db.Users.SingleOrDefault(x => x.Id == id);
            return user;
        }


        /// <summary>
        /// Returns null when user not found
        /// Email only has to have pw in request
        /// </summary>
        public static User FindUser_EMail(this AppDb db, string email)
        {
            User user = null;

            if (!string.IsNullOrEmpty(email))
            {
                user = db.Users.SingleOrDefault(x => x.EMail == email);

            }
            return user;
        }

        /// <summary>
        /// Searches for a user in the database with either the given id or the email.
        /// If the Id is given, it uses that.
        /// If only the EMail is given, it uses that.
        /// If either is not null and the search fails, a UserNotFoundException is thrown.
        /// </summary>
        /// <returns>The found user</returns>
        public static User FindUser(this AppDb db, string id, string email)
        {
            User returnUser = null;
            if (!string.IsNullOrEmpty(id))
            {
                returnUser = db.FindUser_ID(id);
                if (returnUser == null)
                    throw new UserNotFoundException(id);
            }
            else if (!string.IsNullOrEmpty(email))
            {
                returnUser = db.FindUser_EMail(email);
                if (returnUser == null)
                    throw new UserNotFoundException(email);
            }
            return returnUser;
        }
    }
}
