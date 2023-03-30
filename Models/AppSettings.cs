namespace ShoppingListServer.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string DataStorageFolder { get; set; }
        public string UserStorageFolder { get; set; }
        public string NoReplyEMailHost { get; set; }
        public int NoReplyEMailPort { get; set; }
        public string NoReplyEMailAddress { get; set; }
        public string NoReplyEMailPassword { get; set; }
        public string DbServerAddress { get; set; }
        public string DbServerAddressDocker { get; set; }
        public string DbName { get; set; }
        public string DbUser { get; set; }
        public string DbPassword { get; set; }
        public string UseHttpsRedirect { get; set; }
        public string UseDocker { get; set; }
        public string FacebookAppID { get; set; }
        public string AppleClientID { get; set; }
        public string AppleSignInKeyId { get; set; }
        public string AppleTeamId { get; set; }
        public string AppleSignInP8SecretResourcePath { get; set; }
        public string AppleSignInP8SecretPath { get; set; }
    }
}