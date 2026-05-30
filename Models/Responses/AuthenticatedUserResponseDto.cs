namespace ShoppingListServer.Models.Responses
{
    public class AuthenticatedUserResponseDto : UserResponseDto
    {
        public string Token { get; set; }
    }
}
