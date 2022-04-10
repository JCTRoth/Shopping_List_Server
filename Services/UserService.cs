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
using ShoppingListServer.Exceptions;
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

        /// <summary>
        /// Adds a new user by adding a new entry in the database.
        /// </summary>
        /// <exception cref="EMailIInvalidException">If the new users email is invalid (wrong formating/ illegal hosts)</exception>
        /// <exception cref="EMailInUseException">If the new users email is already in use.</exception>
        /// <param name="new_user"></param>
        /// <param name="password">The password of the new user. The passwords hash is stored in the database (not the cleartext password!).</param>
        /// <returns>If succesfull (usually only not succesfull if there is already a user with that id)</returns>
        public bool AddUser(User new_user, string password)
        {
            // Check if the given email is valid (formating / no illegal hosts).
            // The email has to be set (can not be null or empty).
            CheckIfEMailValidException(new_user.EMail);

            // Check if E-Mail already taken and if so, throw exception.
            CheckIfEMailAlreadyInUseException(new_user.EMail);

            // Check if user in list
            // ID
            bool id = _db.Users.Any(user => user.Id == new_user.Id);

            if (id)
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
            User user = FindUser(id, email);

            // return null if user not found
            CheckUserPasswordValidException(user, password);

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

        /// <summary>
        /// Searches for a user in the database with either the given id or the email.
        /// If the Id is given, it uses that.
        /// If only the EMail is given, it uses that.
        /// If either is not null and the search fails, a UserNotFoundException is thrown.
        /// </summary>
        /// <returns>The found user</returns>
        public User FindUser(User user)
        {
            return FindUser(user.Id, user.EMail);
        }

        /// <summary>
        /// Searches for a user in the database with either the given id or the email.
        /// If the Id is given, it uses that.
        /// If only the EMail is given, it uses that.
        /// If either is not null and the search fails, a UserNotFoundException is thrown.
        /// </summary>
        /// <returns>The found user</returns>
        public User FindUser(string id, string email)
        {
            User returnUser = null;
            if (!string.IsNullOrEmpty(id))
            {
                returnUser = FindUser_ID(id);
                if (returnUser == null)
                    throw new UserNotFoundException(id);
            }
            else if (!string.IsNullOrEmpty(email))
            {
                returnUser = FindUser_EMail(email);
                if (returnUser == null)
                    throw new UserNotFoundException(email);
            }
            return returnUser;
        }

        // Changing the email requires a new email verification.
        // To not change a field, just set it null.
        // Changes the following user properties:
        // - FirstName
        // - LastName
        // - UserName
        // - EMail
        // - Color (color can not be set null)
        public bool UpdateUser(string currentUserId, User userUpdate)
        {
            User currentUser = GetById(currentUserId);
            // EMail
            if (currentUser.EMail != userUpdate.EMail)
            {
                // Check if the email is set and it's valid (formating / no illegal hosts).
                CheckIfEMailValidException(userUpdate.EMail);

                // Check if E-Mail already taken and if so, throw exception.
                CheckIfEMailAlreadyInUseException(userUpdate.EMail);

                // now requires new email verification.
                currentUser.EMail = userUpdate.EMail;
                currentUser.IsVerified = false;
            }
            // FirstName
            if (!string.IsNullOrEmpty(userUpdate.FirstName))
                currentUser.FirstName = userUpdate.FirstName;
            // LastName
            if (!string.IsNullOrEmpty(userUpdate.LastName))
                currentUser.LastName = userUpdate.LastName;
            // Username
            if (!string.IsNullOrEmpty(userUpdate.Username))
                currentUser.Username = userUpdate.Username;
            // Color
            if (userUpdate.ColorArgb.HasValue)
                currentUser.ColorArgb = userUpdate.ColorArgb;
            
            _db.SaveChanges();
            return true;
        }

        public bool UpdateUserPassword(string currentUserId, string password)
        {
            User currentUser = GetById(currentUserId);
            HashUserPassword(currentUser, password);
            _db.SaveChanges();
            return true;
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

        public bool AddContact(string currentUserId, User targetUser, UserContactType type)
        {
            targetUser = FindUser(targetUser);
            var contactQuery = from c in _db.Set<UserContact>()
                               where c.UserSourceId == currentUserId && c.UserTargetId == targetUser.Id
                               select c;
            UserContact contact = contactQuery.FirstOrDefault();
            bool success = false;
            if (contact == null)
            {
                _db.Set<UserContact>().Add(new UserContact()
                {
                    UserContactType = type,
                    UserSourceId = currentUserId,
                    UserTargetId = targetUser.Id
                });
                success = true;
            }
            return success;
        }

        public void AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type)
        {
            targetUser = FindUser(targetUser);

            var contactQuery = from c in _db.Set<UserContact>()
                               where c.UserSourceId == currentUserId && c.UserTargetId == targetUser.Id
                               select c;
            UserContact contact = contactQuery.FirstOrDefault();
            if (contact != null)
            {
                contact.UserContactType = type;
            }
            else
            {
                _db.Set<UserContact>().Add(new UserContact()
                {
                    UserContactType = type,
                    UserSourceId = currentUserId,
                    UserTargetId = targetUser.Id
                });
            }
            _db.SaveChanges();
        }

        public bool RemoveContact(string currentUserId, User targetUser)
        {
            targetUser = FindUser(targetUser);
            var contactQuery = from c in _db.Set<UserContact>()
                               where c.UserSourceId == currentUserId && c.UserTargetId == targetUser.Id
                               select c;
            UserContact contact = contactQuery.FirstOrDefault();
            bool success = false;
            if (contact != null)
            {
                _db.Set<UserContact>().Remove(contact);
                _db.SaveChanges();
                success = true;
            }
            return success;
        }

        public List<UserContact> GetContacts(string userId)
        {
            var contactQuery = from c in _db.Set<UserContact>()
                               where c.UserSourceId == userId
                               select c;

            return contactQuery.ToList();
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

        private void CheckUserPasswordValidException(User user, string password)
        {
            if (!IsUserPasswordValid(user, password))
                throw new PasswordIncorrectException(user);
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

        /// <summary>
        /// Checks if the given email is valid (formating / no illegal hosts).
        /// </summary>
        /// <returns>true if it's valid</returns>
        public bool CheckIfEMailValid(string eMail)
        {
            bool isValid = !string.IsNullOrEmpty(eMail);
            if (isValid)
                isValid = new Mail_Tools().Is_Valid_Email(eMail);
            return isValid;
        }

        /// <summary>
        /// Checks if the given email is valid (formatig / no illegal hosts) and if not, throws an EMailIInvalidException.
        /// The email is also invalid if it's null or empty.
        /// </summary>
        /// <exception cref="EMailIInvalidException">thrown if email is invalid</exception>
        /// <param name="eMail">Given email to be checked.</param>
        public void CheckIfEMailValidException(string eMail)
        {
            if (!CheckIfEMailValid(eMail))
                throw new EMailIInvalidException(eMail);
        }

        /// <summary>
        /// Checks if the given E-Mail is already in use (by any user).
        /// The email is also invalid if it's null or empty.
        /// </summary>
        /// <param name="eMail"></param>
        /// <returns></returns>
        public bool CheckIfEMailAlreadyInUse(string eMail)
        {
            var query = from user in _db.Set<User>()
                        where user.EMail == eMail
                        select user;
            return query.Any();
        }

        /// <summary>
        /// Checks if the target e-mail is already in use and if so throws an EMailInUseException.
        /// </summary>
        /// <exception cref="EMailInUseException">If the target e-mail is alreayd in use.</exception>
        public void CheckIfEMailAlreadyInUseException(string eMail)
        {
            if (CheckIfEMailAlreadyInUse(eMail))
                throw new EMailInUseException(eMail);
        }
    }
}