using Microsoft.AspNetCore.Http;

namespace ShoppingListServer.Models.Commands
{
    /// <summary>
    /// Multipart form payload of POST /Users/profile_picture.
    ///
    /// The form fields are bundled into a single model because an action that
    /// mixes <c>[FromForm] IFormFile</c> with other <c>[FromForm]</c> parameters
    /// cannot be described by Swashbuckle (it throws while generating the
    /// OpenAPI document). Model binding is unchanged: the parts are still sent
    /// as the multipart fields "file" and "jsonString".
    /// </summary>
    public class ProfilePictureUploadRequest
    {
        /// <summary>The image file (jpg).</summary>
        public IFormFile File { get; set; }

        /// <summary>The image transformation as an <see cref="ImageInfo"/> JSON string.</summary>
        public string JsonString { get; set; }
    }
}
