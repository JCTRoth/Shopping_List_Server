using ShoppingListServer.Models;
using System;


namespace ShoppingListServer.Exceptions
{
    public class ItemNotFoundException : Exception
    {
        public ItemNotFoundException(string ex_string) :
            base(StatusMessages.ItemNotFound)
        {
        }

    }
}
