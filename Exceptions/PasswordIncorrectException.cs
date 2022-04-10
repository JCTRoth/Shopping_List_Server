using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class PasswordIncorrectException : Exception
    {
        public User User { get; set; }

        public PasswordIncorrectException(User user)
            : base(StatusMessages.PasswordIncorrect)
        {
            User = user;
            Console.WriteLine("PasswordIncorrectException: " + User.EMail + "\n" + StackTrace);
        }
    }
}
