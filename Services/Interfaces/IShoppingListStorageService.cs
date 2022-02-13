using System;
using ShoppingListServer.Models;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IShoppingListStorageService
    {
        ShoppingList Load_ShoppingList(string user_id, string shoppingList_id);

        bool Store_ShoppingList(string user_id, ShoppingList shoppingList);

        bool Update_ShoppingList(string user_id, ShoppingList shoppingList);

        bool Delete_ShoppingList(string user_id, string shoppingList_id);
    };
}
