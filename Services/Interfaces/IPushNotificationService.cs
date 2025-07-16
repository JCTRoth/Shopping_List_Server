using ShoppingListServer.Entities;
using ShoppingListServer.Models.ShoppingData;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IPushNotificationService
    {
        /// <summary>
        /// Inform target user that a list has been shared with them by this user.
        /// </summary>
        Task SendListAdded(User thisUser, User targetUser, string listId);

        /// <summary>
        /// Inform all users that have permission to the list that this user has shared the list with them.
        /// Doesn not inform this user.
        /// </summary>
        Task SendListAdded(User thisUser, string listId, ShoppingListPermissionType permission);
    }
}
