using ShoppingListServer.Models;
using System;


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
