using Newtonsoft.Json;

namespace ShoppingListServer.Models.ShoppingData
{
    /// <summary>
    /// One line item of a shopping list: the relational counterpart of <see cref="GenericProduct"/>.
    ///
    /// The item name, image path and category are stored as flat columns on purpose. A count or
    /// checked change is then a single-row update instead of a read, parse, modify and rewrite of a
    /// whole-list JSON document, and <c>(ShoppingListId, ItemName)</c> can carry a unique index that
    /// makes the "one row per item name" identity explicit.
    ///
    /// <see cref="Position"/> is the authoritative order of the list. It is assigned as 0..n-1 on a
    /// whole-list replace and appended as <c>MAX(Position) + 1</c> when a single product is added.
    /// </summary>
    public class ShoppingListProduct
    {
        public int Id { get; set; }

        public string ShoppingListId { get; set; }

        /// <summary>
        /// The owning list. Navigation only — never serialized.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual ShoppingList ShoppingList { get; set; }

        /// <summary>
        /// Identity of the item within the list. Lists are keyed by item name: there is no stable item
        /// id on the wire and both clients match products by <c>item.name</c>.
        /// </summary>
        public string ItemName { get; set; }

        public string ItemImagePath { get; set; }

        public string CategoryName { get; set; }

        public string CategoryColorHex { get; set; }

        public string CategoryImagePath { get; set; }

        public int Count { get; set; }

        public bool Checked { get; set; }

        public int Position { get; set; }
    }
}
