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
        public static string SomethingWentWrong = "SomethingWentWrong";
        public static string EMailInUse = "EMailInUse"; // "The E-Mail is already in use.";
        public static string EMailInvalid = "EMailInvalid"; //"E-Mail invalid";
        public static string ItemNotFound = "ItemNotFound"; //"The requested item has not been found.";
        public static string MissingListPermission = "MissingListPermission"; // "Operation for this shopping list is not allowed by user.";
        public static string ListNotFound = "ListNotFound"; //"Target shopping list could not be found.";
        public static string UserNotFound = "UserNotFound"; //"Target user could not be found.";
        public static string ContactNotFound = "ContactNotFound";
        public static string PasswordIncorrect = "PasswordIncorrect"; //"The entered password is not correct.";
        public static string PasswordResetCodeExpired = "PasswordResetCodeExpired"; //"Code expired.";
        public static string PasswordResetCodeMissing = "PasswordResetCodeMissing"; //"Wrong Code";
        public static string EMailVerificationLinkExpired = "EMailVerificationLinkExpired"; //"Link expired.";
        public static string UserIsAlreadyVerified = "UserIsAlreadyVerified"; //"User is already verified.";
        public static string UserHasNoEMailAddress = "UserHasNoEMailAddress"; //"User has no E-Mail address.";
        public static string ContactAlreadyAdded = "ContactAlreadyAdded"; //"The user is already in your contact list.";
        public static string CannotAddYourselfAsContact = "CannotAddYourselfAsContact"; //"Can not add yourself as contact.";
        public static string ContactLinkExpired = "ContactLinkExpired"; //"Contact Link expired.";
        public static string ListIsOwnedByBlockedUser = "ListIsOwnedByBlockedUser"; //"The list is owner by a blocked user.";
        public static string NoProfilePictureFound = "NoProfilePictureFound"; //"No profile picture found.";
        public static string AccountAuthenticationFailed = "AccountAuthenticationFailed"; //"Failed to authenticate account.";
        public static string ListShareLinkExpired = "ListShareLinkExpired"; //"The lists share link is expired.";
        public static string ListAlreadyAdded = "ListAlreadyAdded"; //"The list has already been added.";
        public static string ListUpdateFailed = "ListUpdateFailed";
    }
}
