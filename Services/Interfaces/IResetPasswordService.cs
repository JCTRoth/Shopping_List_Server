using ShoppingListServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    /// <summary>
    /// The idea is to only let someone change their password, if
    /// they have access to their email address. This is done by sending
    /// them an email with a code that they have to provide when they want
    /// to change the password.
    /// </summary>
    public interface IResetPasswordService
    {
        /// <summary>
        /// Generates a code for the user of the given email address that can be
        /// used to reset the password by calling
        /// <see cref="SetPassword(string, string, string)"/>
        /// Sends the code as an email to the given email address.
        /// </summary>
        /// <param name="email">email address of user whose password should be changed.</param>
        /// <returns></returns>
        /// <exception cref="UserNotFoundException">If no user with that email address could be found.</exception>
        Task<bool> SendResetPasswordEMailAndAddToken(string userId, string email);

        /// <summary>
        /// Verifies that the given code exists for the given email and if not
        /// throws an exception.
        /// 
        /// Use this method to check if the code the user enterd is correct
        /// before preceeding to a dialog where they are prompted to enter a new password.
        /// In that dialog SetPassword should be called.
        /// </summary>
        /// <exception cref="ResetPasswordCodeMissingException">If the code does not exist.</exception>
        bool CheckIfCodeExistsWithException(string email, string code);

        /// <summary>
        /// Sets the password of the user with the given email but only if
        /// the code matches the one that was generated in
        /// <see cref="SendResetPasswordEMailAndAddToken(string)"/>
        /// </summary>
        /// <returns></returns>
        bool SetPassword(string email, string code, string newPassword);
    }
}
