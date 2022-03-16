using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Models
{
    // Default: neither list sharing nore ignored
    // AllowSharing: allow the user to share lists
    // Ignored: ignore the user
    public enum UserContactType
    {
        Default, AllowSharing, Ignored
    }
}
