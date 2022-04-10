using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class ItemNotFoundException : Exception
    {
        public ItemNotFoundException(string ex_string) :
            base(StatusMessages.ItemNotFound)
        {
            Console.Error.WriteLine("UserNotFoundException " + ex_string + "\n" + StackTrace);
        }

    }
}
