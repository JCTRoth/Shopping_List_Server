namespace ShoppingListServer.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string DbServerAddress { get; set; }
        public string DbServerAddressDocker { get; set; }
        public string DbName { get; set; }
        public string DbUser { get; set; }
        public string DbPassword { get; set; }
        public string UseHttpsRedirect { get; set; }
        public string UseDocker { get; set; }
    }
}