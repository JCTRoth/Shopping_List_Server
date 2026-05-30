namespace ShoppingListServer.Models.Responses
{
    public class UserResponseDto
    {
        public string Id { get; set; }

        public string EMail { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Username { get; set; }

        public int? ColorArgb { get; set; }

        public bool IsVerified { get; set; }
    }
}
