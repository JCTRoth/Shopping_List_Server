using Microsoft.EntityFrameworkCore;
using ShoppingListServer.Entities;

namespace ShoppingListServer.Models
{
    /// <summary>
    /// A token that is used to send notifications to a device using fcm.
    /// A token is connected to a user. There can be multiple tokens
    /// per user but there should only be one token per device.
    /// (A user has multiple tokens if they are logged in on different devices.)
    /// </summary>
    [PrimaryKey(nameof(UserId), nameof(Token))]
    public class FcmToken
    {
        // Foreign key for 1:n relationship
        public string UserId { get; set; }
        public virtual User User { get; set; }

        public string Token { get; set; }
	}
}

