namespace ShoppingListServer.Models
{
    public class Category
    {
        // e.g. : "Category":{"Name":"Getraenke"}
        public string Name { get; set; }

        public string ColorHex { get; set; }

        public string ImagePath { get; set; }
    }
}