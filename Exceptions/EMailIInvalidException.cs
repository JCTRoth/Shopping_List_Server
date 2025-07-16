using ShoppingListServer.Models;
using System;


namespace ShoppingListServer.Exceptions
{
    public class EMailIInvalidException : Exception
    {
        public string EMail { get; set; }
        public EMailIInvalidException(string eMail)
            : base(StatusMessages.EMailInvalid)
        {
            EMail = eMail;
        }
    }
}
