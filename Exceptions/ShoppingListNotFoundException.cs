using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class ShoppingListNotFoundException : Exception
    {
        string ShoppingListId { get; set; }

        public ShoppingListNotFoundException(string _shoppingListId)
            : base(StatusMessages.ShoppingListNotFound)
        {
            ShoppingListId = _shoppingListId;

            Console.Error.WriteLine("ShoppingListNotFoundException " + _shoppingListId + "\n" + StackTrace);
        }
    }
}
