using ShoppingListServer.Entities;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IUserHub
    {
        Task SendUserVerified(User user);

        Task SendContactAdded(string currentUserId, User contactUser);
    }
}
