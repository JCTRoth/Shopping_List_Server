using ShoppingListServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Models
{
    public class UserContact
    {
        public UserContactType UserContactType { get; set; }

        public string UserSourceId { get; set; }
        public virtual User UserSource { get; set; }

        public string UserTargetId { get; set; }
        public virtual User UserTarget { get; set; }
    }
}
