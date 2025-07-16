using ShoppingListServer.Models;
using System;


namespace ShoppingListServer.Exceptions
{
    public class ResetPasswordCodeMissingException : Exception
    {
        public string EMail { get; }
        public string Code { get; }

        public ResetPasswordCodeMissingException(string email, string code)
            : base(StatusMessages.PasswordResetCodeMissing)
        {
            email = EMail;
            code = Code;
        }
    }
}
