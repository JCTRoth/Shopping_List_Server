using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Newtonsoft.Json;
using ShoppingListServer.Entities;
using ShoppingListServer.Models.ShoppingData;

namespace ShoppingListServer.Models
{
    /// <summary>
    /// A shopping list.
    ///
    /// The class doubles as the wire model: the database columns store the metadata (id, owner,
    /// share token, change stamp, version) and the content (name, category, date, notes), while the
    /// line items live in the <see cref="ShoppingListProduct"/> table. <see cref="ProductList"/> is
    /// the <see cref="GenericProduct"/> projection exchanged with the clients, built from
    /// <see cref="Products"/> — never stored as a document.
    /// </summary>
    public class ShoppingList
    {
        private string dateString = string.Empty;

        public ShoppingList()
        {
        }

        // Unique Identity of the ShoppingList.
        // Functions as the bridge between the database, the item rows, and API calls.
        [Key]
        public string SyncId { get; set; }

        /// <summary>
        /// Is set by the server whenever a change to the list was done (item / permission / name / etc.)
        /// If the timestamp of the local list is different from the one on the server, the local list is outdated and needs to be updated.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public DateTime LastChangeServerTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optimistic concurrency token. Incremented together with
        /// <see cref="LastChangeServerTime"/> on every mutation, so two concurrent writers cannot
        /// silently overwrite each other. Never serialized — clients know nothing about it.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public int Version { get; set; }

        /// <summary>
        /// The list owner is the one that created the list. The list is stored under the owners folder.
        /// Owner can't change. Even if the owner doesn't has permissions to their own list, the remain the owner.
        /// This is done to reduce complexity. It's not necessary to reassign owners.
        /// 
        /// Additionally, owners are used to check if a list is blocked which is the case
        /// if the lists owner is blocked by another user. Blocked lists are not send to clients (they don't want to see them).
        /// 
        /// Owners are not relevant for clients. Never assume that a list that was sent by a client contains an owner.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual User Owner { get; set; }

        public string Name { get; set; }

        public string Category { get; set; }

        /// <summary>
        /// The shopping date as an invariant string. An empty string means "no date picked"
        /// (the clients render that as "Unplanned"). Stored verbatim so clients round-trip it unchanged.
        /// </summary>
        public string DateString
        {
            get => dateString;
            set => dateString = value ?? string.Empty;
        }

        // The shopping date. Hours and minutes are stored as well but are ignored.
        // DateTime.MinValue means that no date is picked.
        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public DateTime Date
        {
            get => DateTime.TryParse(DateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
            set => DateString = value == DateTime.MinValue
                ? string.Empty
                : value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public string Notes { get; set; }

        /// <summary>
        /// The line items, in <see cref="ShoppingListProduct.Position"/> order.
        /// The persisted representation; never serialized directly.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<ShoppingListProduct> Products { get; set; } = new List<ShoppingListProduct>();

        /// <summary>
        /// The item projection exchanged with the clients. Populated by the service layer from
        /// <see cref="Products"/>; it is never persisted.
        /// </summary>
        [NotMapped]
        public List<GenericProduct> ProductList { get; set; }

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<ShoppingListPermission> ShoppingListPermissions { get; set; }

        /// <summary>
        /// Id that can be used to share this list with other users.
        /// Not to be confused with the <see cref="SyncId"/>
        /// Neither client reads the token from a list payload; it is only returned by the dedicated
        /// share-id endpoints. Ignoring it also keeps lazy loading from firing during serialization
        /// of detached (read-only) entries.
        /// </summary>
        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual ExpirationToken ShareId { get; set; }

        public void UpdateLastChangeServerTime()
        {
            LastChangeServerTime = DateTime.UtcNow;
            Version++;
        }
    }
}