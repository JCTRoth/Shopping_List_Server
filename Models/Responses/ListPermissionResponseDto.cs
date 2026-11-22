using System.Text.Json.Serialization;

namespace ShoppingListServer.Models.Responses
{
    /// <summary>
    /// One member of a list and their permission, as
    /// <c>GET /shopping/listpermission/{listId}</c> returns it.
    ///
    /// The wire keys are <c>item1</c>/<c>item2</c>: the endpoint historically returned a
    /// <c>Tuple&lt;User, ShoppingListPermissionType&gt;</c>, and both clients (the MAUI app's
    /// <c>List&lt;Tuple&lt;User, string&gt;&gt;</c> and the web client's <c>PermissionEntry</c>)
    /// deserialize that shape. The property names here stay readable; only the JSON keys are the
    /// tuple's.
    /// </summary>
    public class ListPermissionResponseDto
    {
        [JsonPropertyName("item1")]
        public UserResponseDto User { get; set; }

        [JsonPropertyName("item2")]
        public string Permission { get; set; }

        public ListPermissionResponseDto(UserResponseDto user, string permission)
        {
            User = user;
            Permission = permission;
        }
    }
}
