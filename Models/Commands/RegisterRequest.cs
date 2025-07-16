namespace ShoppingListServer.Models.Commands
{
    public class RegisterRequest
    {
        public string EMail { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
