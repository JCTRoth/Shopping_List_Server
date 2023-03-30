using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Models
{
    /// <summary>
    /// Messages that can be expected from the server.
    /// Note: If you change them, the make sure to apply the same changes on the client.
    /// Any changes can lead to wrong behavior of older client versions.
    /// </summary>
    public class StatusMessages
    {
        public static string EMailInUse = "The E-Mail is already in use.";
        public static string EMailInvalid = "E-Mail invalid";
        public static string ItemNotFound = "The requested item has not been found.";
        public static string MissingShoppingListPermission = "Operation for this shopping list is not allowed by user.";
        public static string ShoppingListNotFound = "Target shopping list could not be found.";
        public static string UserNotFound = "Target user could not be found.";
        public static string PasswordIncorrect = "The entered password is not correct.";
        public static string PasswordResetCodeExpired = "Code expired.";
        public static string PasswordResetCodeMissing = "Wrong Code";
        public static string EMailVerificationLinkExpired = "Link expired.";
        public static string UserIsAlreadyVerified = "User is already verified.";
        public static string UserHasNoEMailAddress = "User has no E-Mail address.";
        public static string ContactAlreadyAdded = "The user is already in your contact list.";
        public static string CannotAddYourselfAsContact = "Can not add yourself as contact.";
        public static string ContactLinkExpired = "Contact Link expired.";
        public static string ListIsOwnedByBlockedUser = "The list is owner by a blocked user.";
        public static string NoProfilePictureFound = "No profile picture found.";
        public static string AccountAuthenticationFailed = "Failed to authenticate account.";

        public static string ListShareLinkExpired = "The lists share link is expired.";
        public static string ListAlreadyAdded = "The list has already been added.";
    }
}
