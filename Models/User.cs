using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;

namespace ShoppingListServer.Entities
{
    public class User
    {

        [Key, Required]
        public string Id { get; set; }

        public string EMail { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        // Hashed Password
        public string PasswordHash { get; set; }
        // Salt: part of the hashing algorithm.
        [MaxLength(16)]
        public byte[] Salt { get; set; }

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        // Allow only users to register via the API
        // Value send from request will be ignored -> Default value is applied
        public string Role { get; set; } = "user";

        public string Token { get; set; }

        public bool IsVerified { get; set; } = false;

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<EMailVerificationToken> EMailVerificationTokens { get; set; } = new List<EMailVerificationToken>();

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<ShoppingListPermission> ShoppingListPermissions { get; set; } = new List<ShoppingListPermission>();

        // Does not copy EMailVerificationTokens.
        public User Copy()
        {
            byte[] saltCopy = new byte[Salt.Length];
            System.Array.Copy(Salt, 0, saltCopy, 0, Salt.Length);
            return new User
            {
                Id = Id == null ? null : new string(Id),
                EMail = EMail == null ? null : new string(EMail),
                FirstName = FirstName == null ? null : new string(FirstName),
                LastName = LastName == null ? null : new string(LastName),
                Username = Username == null ? null : new string(Username),
                PasswordHash = Username == null ? null : new string(Username),
                Salt = saltCopy,
                Role = Role == null ? null : new string(Role),
                Token = Token == null ? null : new string(Token),
                IsVerified = IsVerified,
                ShoppingListPermissions = new List<ShoppingListPermission>(ShoppingListPermissions)
            };
        }
    }
}