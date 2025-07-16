using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;
using System;


namespace ShoppingListServer.Exceptions
{
    public class NoShoppingListPermissionException : Exception
    {
        public NoShoppingListPermissionException(ShoppingListPermission _permission, ShoppingListPermissionType _expectedPermission)
            : base(StatusMessages.MissingListPermission)
        {
            Permission = _permission;
            ExpectedPermission = _expectedPermission;
        }

        public ShoppingListPermission Permission { get; set; }
        public ShoppingListPermissionType ExpectedPermission { get; set; }
    }
}
