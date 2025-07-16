using ShoppingListServer.Models;
using System;


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
