using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.Models;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class UserService : IUserService
    {
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;
        private readonly IUserHub _userHub;
        private readonly IFilesystemService _filesystemService;

        public UserService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserHub userHub,
            IFilesystemService filesystemService)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _userHub = userHub;
            _filesystemService = filesystemService;
        }

        public bool AddUser(User new_user, string password)
        {
            // When Email address was set, than check if valid
            if(! string.IsNullOrEmpty(new_user.EMail))
            {
                if (! new Mail_Tools().Is_Valid_Email(new_user.EMail))
                {
                    // Not Valid
                    return false;
                }
            }

            // Check if user in list
            // ID
            bool id = _db.Users.Any(user => user.Id == new_user.Id);
            // EMail
            bool email = _db.Users.Any(user => user.EMail == new_user.EMail);

            if(id || email)
            {
                // User is already in List
                return false; 
            }

            // Add User to list
            if (_filesystemService.CreateUserFolder(new_user.Id))
            {
                HashUserPassword(new_user, password);

                _db.Users.Add(new_user);
                _db.SaveChanges();
                return true;
            }

            return false;
        }

        // Authenticates the user.
        // If the given id is null or "", uses the email to identify the user.
        public Result Authenticate(string id, string email, string password)
        {
            Result result = new Result();
            User user = null;

            if (!string.IsNullOrEmpty(id))
            {
                user = FindUser_ID(id);
            }
            else if (!string.IsNullOrEmpty(email))
            {
                user = FindUser_EMail(email);
            }

            // return null if user not found
            if (user == null || !IsUserPasswordValid(user, password))
            {
                result.WasFound = false;
                return result;
            }

            // authentication successful so generate jwt token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[] 
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.EMail),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                // TODO Expire Token
                Expires = DateTime.UtcNow.AddDays(99999),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            user.Token = tokenHandler.WriteToken(token);
            _db.SaveChanges();

            result.WasFound = true;
            result.ReturnValue = user.WithoutPassword();
            return result;
        }

        // Returns null when user not found
        // Users without password set will have pw null
        // Requests without password field will have value null 
        private User FindUser_ID(string id)
        {
            var user = _db.Users.SingleOrDefault(x => x.Id == id);
            return user;
        }

        // Returns null when user not found
        // Email only has to have pw in request
        private User FindUser_EMail(string email)
        {
            User user = null;

            if (!string.IsNullOrEmpty(email))
            {
                user = _db.Users.SingleOrDefault(x => x.EMail == email);
                
            }
            return user;
        }

        public IEnumerable<User> GetAll()
        {
            return _db.Users.WithoutPasswords();
        }

        public User GetById(string id) 
        {
            var user = _db.Users.FirstOrDefault(x => x.Id == id);
            return user;
        }

        public User GetByEMail(string email)
        {
            User user = _db.Users.FirstOrDefault(x => x.EMail.Equals(email));
            return user;
        }

        // Checks if the users password hash matches with the given clear text passwords hash.
        // \param password - clear text password
        private bool IsUserPasswordValid(User user, string password)
        {
            if (password == null || user.Salt == null)
                return false;
            string passwordHash = HashString(user.Salt, password);
            return user.PasswordHash.Equals(passwordHash);
        }

        // Hashes the given password and stores the hash along with its salt in the database.
        // Later, user IsUserPasswordValid to check the validity of the password.
        private void HashUserPassword(User user, string password)
        {
            // Salt length of 128 bit is recommended by the US National Institute of Standards and Technology, see https://en.wikipedia.org/wiki/PBKDF2
            user.Salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(user.Salt);
            }

            user.PasswordHash = HashString(user.Salt, password);
        }

        // Hash the given string using PBKDF2 with SHA​-512 and a recommended 128 bit (16 byte) long salt.
        private string HashString(byte[] salt, string value)
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