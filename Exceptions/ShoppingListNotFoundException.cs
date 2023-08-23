using ShoppingListServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Exceptions
{
    public class ShoppingListNotFoundException : Exception
    {
        public string ShoppingListId { get; set; }

        public ShoppingListNotFoundException(string _shoppingListId)
            : base(StatusMessages.ListNotFound)
        {
            ShoppingListId = _shoppingListId;
        }
    }
}
