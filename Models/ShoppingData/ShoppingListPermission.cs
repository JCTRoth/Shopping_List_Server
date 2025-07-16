using ShoppingListServer.Entities;

namespace ShoppingListServer.Models.ShoppingData
{
    public class ShoppingListPermission
    {
        public ShoppingListPermissionType PermissionType { get; set; }

        public string ShoppingListId { get; set; }
        public virtual ShoppingList ShoppingList { get; set; }

        public string UserId { get; set; }
        public virtual User User { get; set; }
        
    }
}
