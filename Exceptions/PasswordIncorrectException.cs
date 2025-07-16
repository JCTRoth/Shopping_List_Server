using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using System;


namespace ShoppingListServer.Exceptions
{
    public class PasswordIncorrectException : Exception
    {
        public User User { get; set; }

        public PasswordIncorrectException(User user)
            : base(StatusMessages.PasswordIncorrect)
        {
            User = user;
        }
    }
}
