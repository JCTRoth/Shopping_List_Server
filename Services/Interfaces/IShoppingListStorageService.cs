using ShoppingListServer.Models;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IShoppingListStorageService
    {
        ShoppingList Load_ShoppingList(string user_id, string shoppingList_id);

        bool Store_ShoppingList(string user_id, ShoppingList shoppingList);

        bool Update_ShoppingList(string user_id, ShoppingList shoppingList);

        bool Move_ShoppingList(string user_id_old, string user_id_new, string shoppingList_id);

        bool Delete_ShoppingList(string user_id, string shoppingList_id);
    };
}
