namespace ShoppingListServer.Models.Commands
{
    public class ImageTransformationDTO
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Scale { get; set; }
        public double Rotation { get; set; }

        public ImageTransformationDTO(int x, int y, double scale, double rotation)
        {
            X = x;
            Y = y;
            Scale = scale;
            Rotation = rotation;
        }
    }
}
