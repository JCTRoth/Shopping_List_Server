using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
