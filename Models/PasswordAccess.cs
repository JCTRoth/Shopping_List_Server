using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using ShoppingListServer.Entities;

namespace ShoppingListServer.Models
{
    public class PasswordAccess
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        // Foreign key for 1:n relationship
        public string UserId { get; set; }
        public virtual User User { get; set; }

        // Hashed Password
        public string PasswordHash { get; set; }
        // Salt: part of the hashing algorithm.
        [MaxLength(16)]
        public byte[] Salt { get; set; }

        public static PasswordAccess Generate(string password)
        {
            PasswordAccess passwordAccess = new PasswordAccess();
            passwordAccess.Salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(passwordAccess.Salt);
            }
            passwordAccess.PasswordHash = HashString(passwordAccess.Salt, password);
            return passwordAccess;
        }

        public static bool IsPasswordValid(byte[] salt, string sourcePasswordHash, string targetPassword)
        {
            if (string.IsNullOrEmpty(targetPassword) || salt == null)
                return false;
            string passwordHash = PasswordAccess.HashString(salt, targetPassword);
            return sourcePasswordHash.Equals(passwordHash);
        }

        // Hash the given string using PBKDF2 with SHA​-512 and a recommended 128 bit (16 byte) long salt.
        public static string HashString(byte[] salt, string value)
        {
            // 32 bytes requested is sufficient for password hashing, see https://security.stackexchange.com/a/58450
            // https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-5.0
            return Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: value,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA512,
                iterationCount: 10000,
                numBytesRequested: 32));
        }
    }
}
