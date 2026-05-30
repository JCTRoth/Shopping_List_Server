using System.Collections.Generic;
using System.Linq;
using ShoppingListServer.Entities;
using ShoppingListServer.Models.Responses;

namespace ShoppingListServer.Helpers
{
    public static class ExtensionMethods
    {
        // Creates a copies of the given users with password == null.
        public static IEnumerable<User> WithoutPasswords(this IEnumerable<User> users) 
        {
            if (users == null)
                return null;

            return users.Select(x => x.WithoutPassword());
        }

        // Creates a copy of the given user with password == null.
        public static User WithoutPassword(this User user) 
        {
            if (user == null)
                return null;

            user = user.Copy();
            user.PasswordHash = null;
            user.Salt = null;
            user.Token = null;
            user.Role = null;
            user.ExternalId = null;
            user.ContactShareId = null;
            return user;
        }

        public static UserResponseDto ToUserResponse(this User user)
        {
            if (user == null)
                return null;

            return new UserResponseDto
            {
                Id = user.Id,
                EMail = user.EMail,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                ColorArgb = user.ColorArgb,
                IsVerified = user.IsVerified
            };
        }

        public static IEnumerable<UserResponseDto> ToUserResponses(this IEnumerable<User> users)
        {
            if (users == null)
                return null;

            return users.Select(x => x.ToUserResponse());
        }

        public static AuthenticatedUserResponseDto ToAuthenticatedUserResponse(this User user)
        {
            if (user == null)
                return null;

            return new AuthenticatedUserResponseDto
            {
                Id = user.Id,
                EMail = user.EMail,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                ColorArgb = user.ColorArgb,
                IsVerified = user.IsVerified,
                Token = user.Token
            };
        }
    }
}