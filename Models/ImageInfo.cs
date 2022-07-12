using ShoppingListServer.Models.Commands;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoppingListServer.Models
{
    /// <summary>
    /// Stores image transformation and dates when the image was last changed.
    /// </summary>
    public class ImageInfo
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public int X { get; set; }
		public int Y { get; set; }
		public double Scale { get; set; }
		public double Rotation { get; set; }

		/// <summary>
		/// When X, Y, Scale, or Rotation changed.
		/// </summary>
		public DateTime LastChangeTransformationTime { get; set; }
		
		/// <summary>
		/// When the image file changed -> requires downloading the new file.
		/// </summary>
		public DateTime LastChangeImageFileTime { get; set; }

        public ImageInfo()
        {

        }

        public ImageInfo(int x, int y, double scale, double rotation, DateTime lastChangeTransformTime, DateTime lastChangeImageFileTime)
        {
            X = x;
            Y = y;
            Scale = scale;
            Rotation = rotation;
            LastChangeTransformationTime = lastChangeTransformTime;
            LastChangeImageFileTime = lastChangeImageFileTime;
        }

        public ImageInfo(ImageTransformationDTO trans)
        {
            ApplyChanges(trans);
        }

        public void ApplyChanges(ImageInfo info)
        {
            X = info.X;
            Y = info.Y;
            Scale = info.Scale;
            Rotation = info.Rotation;
            LastChangeTransformationTime = info.LastChangeTransformationTime;
            LastChangeImageFileTime = info.LastChangeImageFileTime;
        }

        public void ApplyChanges(ImageTransformationDTO trans)
        {
            X = trans.X;
            Y = trans.Y;
            Scale = trans.Scale;
            Rotation = trans.Rotation;
        }
    }
}
