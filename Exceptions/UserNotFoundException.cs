using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string ex_string)
            : base(StatusMessages.UserNotFound)
        {
        }
    }
}
