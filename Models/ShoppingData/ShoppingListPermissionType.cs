using System;

namespace ShoppingListServer.Models.ShoppingData
{

    /// <summary>
    /// Fine flags with 1 << x: https://stackoverflow.com/a/1030115
    /// Combine flags: https://stackoverflow.com/a/1030103
    /// Read - allow read access of list and list properties
    /// Write - allow write access of list entries
    /// Delete - allow the deletion of the list
    /// AddPermission - allows to add a permission (but not change or remove it)
    /// ModifyPermission - allow to add/remove/change member permissions
    /// WriteAndAddPermission - write list and add members -> default permission
    /// WriteAndModifyPermission - write list and add/remove/modify member permission -> advanced access
    /// All - all of the above
    /// </summary>
    [Flags]
    public enum ShoppingListPermissionType
    {
        Undefined = 0,                                      // 00000
        Read = 1 << 0,                                      // 00001
        Write = 1 << 1 | Read,                              // 00011
        Delete = 1 << 2,                                    // 00100
        AddPermission = 1 << 3,                             // 01000
        ModifyPermission = 1 << 4 | AddPermission,          // 11000
        WriteAndAddPermission = Write | AddPermission,      // 01011
        WriteAndModifyPermission = Write | ModifyPermission,// 11011
        All = Write | Delete | ModifyPermission             // 11111
    }

}
