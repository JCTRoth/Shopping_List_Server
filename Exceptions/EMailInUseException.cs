using ShoppingListServer.Models;
using System;


namespace ShoppingListServer.Exceptions
{
    public class EMailInUseException : Exception
    {
        public string EMail { get; set; }

        public EMailInUseException(string eMail) :
            base(StatusMessages.EMailInUse)
        {
            EMail = eMail;
        }
    }
}
