using ShoppingListServer.Models;

namespace ShoppingListServer.Models.Responses
{
    public class UserContactResponseDto
    {
        public UserResponseDto User { get; set; }

        public UserContactType Type { get; set; }

        public UserContactResponseDto(UserResponseDto user, UserContactType type)
        {
            User = user;
            Type = type;
        }
    }
}
