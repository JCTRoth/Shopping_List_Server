using ShoppingListServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Models.Commands
{
    // User contact without the data base ids.
    public class UserContactDTO
    {
        public User User { get; set; }
        public UserContactType Type { get; set; }

        public UserContactDTO(User user, UserContactType type)
        {
            User = user;
            Type = type;
        }
    }
}
