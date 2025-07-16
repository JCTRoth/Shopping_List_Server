namespace ShoppingListServer.Models.Commands
{
    public class Update_ShoppingListPermission
    {
        public string ListId { get; set; }
        public string Username { get; set; }
        public string Permission { get; set; }
    }
}
