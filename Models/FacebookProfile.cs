using Newtonsoft.Json;

namespace ShoppingListServer.Models
{
    public class FacebookProfile
    {
        public string Email { get; set; }
        public string Id { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }
        [JsonProperty("first_name")]
        public string FirstName { get; set; }
    }
}
