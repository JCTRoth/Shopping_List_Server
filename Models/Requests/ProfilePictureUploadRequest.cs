using Microsoft.AspNetCore.Http;

namespace ShoppingListServer.Models.Requests
{
    public class ProfilePictureUploadRequest
    {
        public IFormFile File { get; set; }

        public string JsonString { get; set; }
    }
}
