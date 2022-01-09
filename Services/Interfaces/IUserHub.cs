using ShoppingListServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IUserHub
    {
        Task SendUserVerified(User user);
    }
}
