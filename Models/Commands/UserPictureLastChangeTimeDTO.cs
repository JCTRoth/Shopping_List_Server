using System;

namespace ShoppingListServer.Models.Commands
{
    public class UserPictureLastChangeTimeDTO
    {
        public string UserId { get; set; }
        
        /// <summary>
        /// When X, Y, Scale, or Rotation changed.
        /// </summary>
        public DateTime TransformationLastChangeTime { get; set; }
        
        /// <summary>
        /// When the image file changed -> requires downloading the new file.
        /// </summary>
        public DateTime ImageFileLastChangeTime { get; set; }

        public UserPictureLastChangeTimeDTO(string userId, DateTime transformationLastChangeTime, DateTime imageFileLastChangeTime)
        {
            UserId = userId;
            TransformationLastChangeTime = transformationLastChangeTime;
            ImageFileLastChangeTime = imageFileLastChangeTime;
        }
    }
}
