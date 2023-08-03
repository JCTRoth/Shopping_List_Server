using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShoppingListServer.Database;
using ShoppingListServer.Entities;
using ShoppingListServer.Exceptions;
using ShoppingListServer.Helpers;
using ShoppingListServer.Logic;
using ShoppingListServer.Models;
using ShoppingListServer.Models.Commands;
using ShoppingListServer.Models.ShoppingData;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    public class UserService : IUserService
    {
        private readonly AppSettings _appSettings;
        private readonly AppDb _db;
        private readonly IUserHub _userHub;
        private readonly IFilesystemService _filesystemService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IEMailVerificationService _emailVerificationService;

        public UserService(
            IOptions<AppSettings> appSettings,
            AppDb db,
            IUserHub userHub,
            IFilesystemService filesystemService,
            IAuthenticationService authenticationService,
            IEMailVerificationService emailVerificationService)
        {
            _appSettings = appSettings.Value;
            _db = db;
            _userHub = userHub;
            _filesystemService = filesystemService;
            _authenticationService = authenticationService;
            _emailVerificationService = emailVerificationService;
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
            return AddUser(new_user, password, false);
        }

        private bool AddUser(User new_user, string password, bool isAlternativePassword)
        {
            // Check if the given email is valid (formating / no illegal hosts).
            // The email has to be set (can not be null or empty).
            CheckIfEMailValidException(new_user.EMail);

            // Check if E-Mail already taken and if so, throw exception.
            if (!isAlternativePassword)
            {
                CheckIfEMailAlreadyInUseException(new_user.EMail);
            }

            // Check if user in list
            // ID
            new_user.Id = Guid.NewGuid().ToString();
            bool id = _db.Users.Any(user => user.Id == new_user.Id);

            if (id)
            {
                // User is already in List
                return false;
            }

            // Add User to list
            if (_filesystemService.CreateUserFolder(new_user.Id))
            {
                if (isAlternativePassword)
                {
                    AddAlternativeHashedPassword(new_user, password);
                }
                else
                {
                    AddUserPassword(new_user, password);
                }
                _db.Users.Add(new_user);
                _db.SaveChanges();
                return true;
            }

            return false;
        }

        private bool AddUserOrAlternativePassword(User new_user, string password)
        {
            User user = FindUserTryExternalIdFirst(new_user, false);
            bool success;
            if (user != null)
            {
                new_user.Id = user.Id;
                if (!string.IsNullOrEmpty(new_user.ExternalId))
                    user.ExternalId = new_user.ExternalId;
                success = AddAlternativeHashedPassword(user, password);
            }
            else
            {
                success = AddUser(new_user, password, true);
            }
            return success;
        }

        private User AddSocialMediaVerifiedUser(User new_user, string password)
        {
            AddUserOrAlternativePassword(new_user, password);
            User user = FindUserTryExternalIdFirst(new_user);
            // User is verified via access token which is done in the *WithException call above.
            if (user != null)
            {
                user.IsVerified = true;
                _db.SaveChanges();
            }
            return user;
        }

        public async Task<User> RegisterAppleUser(AppleAccount appleAccount, string password)
        {
            // verify access token
            await _authenticationService.AuthenticateAppleWithException(appleAccount);

            // Add user or alternative password.
            User new_user = new User
            {
                EMail = appleAccount.Email,
                Username = appleAccount.Name,
                ExternalId = appleAccount.UserId
            };
            return AddSocialMediaVerifiedUser(new_user, password);
        }

        public async Task<User> RegisterGoogleUser(GoogleUser googleUser, string accessToken, string password)
        {
            // verify access token
            await _authenticationService.AuthenticateGoogleWithException(googleUser, accessToken);

            // Add user or alternative password.
            User new_user = new User
            {
                EMail = googleUser.Email,
                Username = googleUser.Name,
                ExternalId = googleUser.Id
            };
            return AddSocialMediaVerifiedUser(new_user, password);
        }

        public async Task<User> RegisterFacebookUser(FacebookProfile facebookProfile, string accessToken, string password)
        {
            // verify access token
            await _authenticationService.AuthenticateFacebookWithException(facebookProfile, accessToken);

            // Add user or alternative password.
            User new_user = new User
            {
                EMail = facebookProfile.Email,
                Username = facebookProfile.FirstName,
                ExternalId = facebookProfile.Id
            };
            return AddSocialMediaVerifiedUser(new_user, password);
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

        private User FindUser_ExternalId(string externalId)
        {
            User user = null;
            if (!string.IsNullOrEmpty(externalId))
            {
                user = _db.Users.SingleOrDefault(x => x.ExternalId == externalId);

            }
            return user;
        }

        private User FindUserTryExternalIdFirst(User user, bool throwException = true)
        {
            User foundUser = FindUser_ExternalId(user.ExternalId);
            if (foundUser == null)
            {
                foundUser = FindUser(user, throwException);
            }
            return foundUser;
        }

        /// <summary>
        /// Searches for a user in the database with either the given id or the email.
        /// If the Id is given, it uses that.
        /// If only the EMail is given, it uses that.
        /// If either is not null and the search fails, a UserNotFoundException is thrown.
        /// </summary>
        /// <returns>The found user</returns>
        public User FindUser(User user, bool throwException = true)
        {
            return FindUser(user.Id, user.EMail, throwException);
        }

        /// <summary>
        /// Searches for a user in the database with either the given id or the email.
        /// If the Id is given, it uses that.
        /// If only the EMail is given, it uses that.
        /// If either is not null and the search fails, a UserNotFoundException is thrown.
        /// </summary>
        /// <exception cref="UserNotFoundException">If the user could not be found.</exception>
        /// <returns>The found user</returns>
        public User FindUser(string id, string email, bool throwException = true)
        {
            User returnUser = null;
            if (!string.IsNullOrEmpty(id))
            {
                returnUser = FindUser_ID(id);
            }
            if (returnUser == null && !string.IsNullOrEmpty(email))
            {
                returnUser = FindUser_EMail(email);
            }
            if (throwException && returnUser == null)
                throw new UserNotFoundException(id);
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
            AddUserPassword(currentUser, password);
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

        public async Task<bool> AddOrUpdateContact(string currentUserId, User targetUser, UserContactType type, bool allowUpdate)
        {
            targetUser = FindUser(targetUser);

            var contactQuery = from c in _db.Set<UserContact>()
                               where c.UserSourceId == currentUserId && c.UserTargetId == targetUser.Id
                               select c;
            UserContact contact = contactQuery.FirstOrDefault();
            if (contact != null)
            {
                if (allowUpdate)
                {
                    contact.UserContactType = type;
                }
                else
                {
                    throw new Exception(StatusMessages.ContactAlreadyAdded);
                }
            }
            else
            {
                _db.Set<UserContact>().Add(new UserContact()
                {
                    UserContactType = type,
                    UserSourceId = currentUserId,
                    UserTargetId = targetUser.Id
                });
                await _userHub.SendContactAdded(currentUserId, targetUser);
            }
            _db.SaveChanges();
            return true;
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

        public void RegisterFcmToken(string currentUserId, string fcmToken)
        {
            User user = FindUser(currentUserId, null);
            user.FcmTokens.Add(new FcmToken() { Token = fcmToken });
            _db.SaveChanges();
        }

        public void UnregisterFcmToken(string currentUserId, string fcmToken)
        {
            User user = FindUser(currentUserId, null);
            user.FcmTokens.RemoveAll(x => x.Token == fcmToken);
            _db.SaveChanges();
        }

        /// <summary>
        /// Checks if the users password or any of the alternative passwords
        /// matches with the given clear text passwords hash.
        /// <param name="user"></param>
        /// <param name="password">clear text password</param>
        /// <returns>If the password matches</returns>
        private bool IsUserPasswordValid(User user, string password)
        {
            if (PasswordAccess.IsPasswordValid(user.Salt, user.PasswordHash, password))
                return true;
            foreach (PasswordAccess access in user.AlternativePasswords)
            {
                if (PasswordAccess.IsPasswordValid(access.Salt, access.PasswordHash, password))
                {
                    return true;
                }
            }
            return false;
        }

        private void CheckUserPasswordValidException(User user, string password)
        {
            if (!IsUserPasswordValid(user, password))
                throw new PasswordIncorrectException(user);
        }

        // Hashes the given password and stores the hash along with its salt in the database.
        // Later, user IsUserPasswordValid to check the validity of the password.
        public void AddUserPassword(User user, string password)
        {
            PasswordAccess passwordAccess = PasswordAccess.Generate(password);
            user.Salt = passwordAccess.Salt;
            user.PasswordHash = passwordAccess.PasswordHash;
        }

        private bool AddAlternativeHashedPassword(User user, string password)
        {
            // check if password already exists
            //if (IsUserPasswordValid(user, password))
            //{
            //    return false;
            //}
            //foreach (PasswordAccess access in user.AlternativePasswords)
            //{
            //    if (PasswordAccess.IsPasswordValid(access.Salt, access.PasswordHash, password))
            //    {
            //        return false;
            //    }
            //}

            PasswordAccess passwordAccess = PasswordAccess.Generate(password);
            user.AlternativePasswords.Add(passwordAccess);
            _db.SaveChanges();

            return true;
        }

        public string GenerateOrExtendContactShareId(string currentUserId)
        {
            User thisUser = FindUser(currentUserId, null);
            if (thisUser.ContactShareId == null || thisUser.ContactShareId.IsExpired())
            {
                thisUser.ContactShareId = new ExpirationToken()
                {
                    ExpirationTime = DateTime.UtcNow.AddDays(2),
                    Data = RandomKeyFactory.GetUniqueKeyOriginal_BIASED(32)
                };
            }
            else
            {
                thisUser.ContactShareId.ExpirationTime = DateTime.UtcNow.AddDays(2);
            }
            _db.SaveChanges();
            return thisUser.ContactShareId.Data;
        }

        public async Task<User> AddUserFromContactShareId(string currentUserId, string contactShareId)
        {
            User thisUser = FindUser(currentUserId, null);
            var query = from user in _db.Set<User>()
                        where user.ContactShareId != null && user.ContactShareId.Data == contactShareId
                        select user;
            User targetUser = query.FirstOrDefault();
            if (targetUser == null)
            {
                throw new Exception(StatusMessages.UserNotFound);
            }
            else if (targetUser.ContactShareId.IsExpired())
            {
                throw new Exception(StatusMessages.ContactLinkExpired);
            }
            else if (targetUser == thisUser)
            {
                throw new Exception(StatusMessages.CannotAddYourselfAsContact);
            }
            else
            {
                // Don't throw an exception if the other one already added you as contact (pass "true" here).
                await AddOrUpdateContact(targetUser.Id, thisUser, UserContactType.Default, true);
                // Only throw the exception if you already added the user (pass "false" here).
                await AddOrUpdateContact(thisUser.Id, targetUser, UserContactType.Default, false);
            }
            return targetUser;
        }

        /// <summary>
        /// Checks if the given email is valid (formating / no illegal hosts).
        /// </summary>
        /// <returns>true if it's valid</returns>
        private bool CheckIfEMailValid(string eMail)
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
        private void CheckIfEMailValidException(string eMail)
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
        private bool CheckIfEMailAlreadyInUse(string eMail)
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
        private void CheckIfEMailAlreadyInUseException(string eMail)
        {
            if (CheckIfEMailAlreadyInUse(eMail))
                throw new EMailInUseException(eMail);
        }

        public async Task<bool> AddOrUpdateProfilePicture(string currentUserId, IFormFile picture, ImageInfo info)
        {
            User user = FindUser(currentUserId, null);

            // Update database.
            //DateTime lastChange = DateTime.Now;
            if (user.ProfilePictureInfo == null)
            {
                user.ProfilePictureInfo = new ImageInfo();
            }
            user.ProfilePictureInfo.ApplyChanges(info);
            //user.ProfilePictureInfo.LastChangeTransformationTime = lastChange;
            //user.ProfilePictureInfo.LastChangeImageFileTime = lastChange;
            _db.SaveChanges();

            // Copy file.
            string filePath = GetProfilePicturePath(currentUserId);
            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                await picture.CopyToAsync(stream);
            }
            return true;
        }

        public void UpdateProfilePictureTransformation(string currentUserId, ImageTransformationDTO trans)
        {
            User user = FindUser(currentUserId, null);

            // Update database.
            // Only update LastChangeTransformationTime, not LastChangeImageFileTime.
            DateTime lastChange = DateTime.Now;
            if (user.ProfilePictureInfo == null)
            {
                user.ProfilePictureInfo = new ImageInfo();
            }
            user.ProfilePictureInfo.ApplyChanges(trans);
            user.ProfilePictureInfo.LastChangeTransformationTime = lastChange;
            _db.SaveChanges();
        }

        public bool RemoveProfilePicture(string currentUserId)
        {
            User user = FindUser(currentUserId, null);

            // Remove database entry.
            user.ProfilePictureInfo = null;
            _db.SaveChanges();

            // Remove image file.
            string filePath = GetProfilePicturePath(currentUserId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }

            return false;
        }

        public ImageInfo GetProfilePictureInfo(string currentUserId)
        {
            User user = FindUser(currentUserId, null);
            if (user.ProfilePictureInfo != null)
            {
                string filePath = GetProfilePicturePath(currentUserId);
                if (File.Exists(filePath))
                {
                    return user.ProfilePictureInfo;
                }
                else
                {
                    Console.WriteLine("ERROR: Profile picture file not found although ProfilePictureInfo is present.");
                    user.ProfilePictureInfo = null;
                    _db.SaveChanges();
                }
            }
            throw new Exception(StatusMessages.NoProfilePictureFound);
        }

        public async Task<byte[]> GetProfilePicture(string currentUserId)
        {
            User user = FindUser(currentUserId, null);
            if (user.ProfilePictureInfo != null)
            {
                string filePath = GetProfilePicturePath(currentUserId);
                if (File.Exists(filePath))
                {
                    return await File.ReadAllBytesAsync(filePath);
                }
                else
                {
                    Console.WriteLine("ERROR: Profile picture file not found although ProfilePictureInfo is present.");
                    user.ProfilePictureInfo = null;
                    _db.SaveChanges();
                }
            }
            throw new Exception(StatusMessages.NoProfilePictureFound);
        }

        /// <summary>
        /// Find all the last change times of the relevant users.
        /// Relevant users are those that can be seen by this user.
        /// They are in the contact list of this user or in any shared list.
        /// </summary>
        /// <param name="currentUserId"></param>
        /// <returns></returns>
        public List<UserPictureLastChangeTimeDTO> GetUserPictureLastChangeTimes(string currentUserId)
        {
            User thisUser = GetById(currentUserId);

            // this user
            var thisUserQuery = from u in _db.Set<User>()
                                where u.Id == currentUserId && u.ProfilePictureInfo != null
                                select u;

            // users from contacts
            var contactUsersQuery = from c in _db.Set<UserContact>()
                                    where c.UserSourceId == currentUserId && c.UserTarget.ProfilePictureInfo != null
                                    select c.UserTarget;
            // users from shared lists
            var sharedLists = from perm in _db.Set<ShoppingListPermission>()
                              where perm.UserId == currentUserId
                              select perm.ShoppingList;
            var sharedUsers = from list in sharedLists
                              join perm in _db.Set<ShoppingListPermission>() on list.SyncId equals perm.ShoppingListId
                              where perm.UserId != currentUserId && perm.User.ProfilePictureInfo != null
                              select perm.User;

            // concat all queries together
            var users = thisUserQuery.Concat(contactUsersQuery).Concat(sharedUsers);

            // remove duplicates and create UserPictureLastChangeTimeDTO
            var times = from user in users.Distinct()
                        select new UserPictureLastChangeTimeDTO(user.Id, user.ProfilePictureInfo.LastChangeTransformationTime, user.ProfilePictureInfo.LastChangeImageFileTime);

            return times.ToList();
        }

        private string GetProfilePicturePath(string userId)
        {
            string pictureName = GetProfilePictureFilename(userId);
            return System.IO.Path.Combine(_filesystemService.GetUserFolderPath(userId), pictureName);
        }

        private string GetProfilePictureFilename(string userId)
        {
            return string.Format("profile_picture_{0}.jpg", userId.Substring(userId.Length - 8, 8));
        }
    }
}