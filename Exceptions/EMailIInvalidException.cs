using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class EMailIInvalidException : Exception
    {
        public string EMail { get; set; }
        public EMailIInvalidException(string eMail)
            : base(StatusMessages.EMailInvalid)
        {
            EMail = eMail;
            Console.WriteLine("EMailInvalidException " + Message + " " + eMail + "\n" + StackTrace);
        }
    }
}
