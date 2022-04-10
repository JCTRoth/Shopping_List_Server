using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class EMailInUseException : Exception
    {
        public string EMail { get; set; }

        public EMailInUseException(string eMail) :
            base(StatusMessages.EMailInUse)
        {
            EMail = eMail;
            Console.Error.WriteLine("EMailInUserException " + Message + " : " + eMail + "\n" + StackTrace);
        }
    }
}
