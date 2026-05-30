namespace ShoppingListServer.Models.Responses
{
    public class ListPermissionResponseDto
    {
        public UserResponseDto User { get; set; }

        public string Permission { get; set; }

        public ListPermissionResponseDto(UserResponseDto user, string permission)
        {
            User = user;
            Permission = permission;
        }
    }
}
