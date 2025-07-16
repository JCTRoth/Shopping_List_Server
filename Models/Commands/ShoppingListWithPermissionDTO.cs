using ShoppingListServer.Models.ShoppingData;

namespace ShoppingListServer.Models.Commands
{
    public class ShoppingListWithPermissionDTO
    {
        public ShoppingListWithPermissionDTO(ShoppingList list, string userId, ShoppingListPermissionType permission)
        {
            List = list;
            UserId = userId;
            Permission = permission;
        }

        public ShoppingList List { get; set; }
        public string UserId { get; set; }
        public ShoppingListPermissionType Permission { get; set; }
    }
}
