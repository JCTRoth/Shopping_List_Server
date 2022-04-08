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
    public class ShoppingList
    {
        private DateTime date;
        private string dateString;

        public ShoppingList()
        {
        }

        // Unique Identity of the ShoppingList
        // Functions as bridge between Json files, Database, and API calls.
        [Key]
        public string SyncId { get; set; }

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

        [NotMapped]
        public string Name { get; set; }

        [NotMapped]
        public string Category { get; set; }

        [NotMapped]
        public string DateString
        {
            get => dateString;
            set
            {
                dateString = value;
                if (DateString == null || DateString.Equals(""))
                    date = DateTime.MinValue;
                else
                    date = DateTime.Parse(DateString, CultureInfo.InvariantCulture);
            }
        }

        // The shopping date. Hours and mintes are stored as well but are ignored.
        // DateTime.MinValue means that no date is picked.
        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public DateTime Date
        {
            get => date;
            set
            {
                date = value;
                dateString = Date.ToString(CultureInfo.InvariantCulture);
            }
        }

        [NotMapped]
        public string Notes { get; set; }

        [NotMapped]
        public List<GenericProduct> ProductList { get; set; }

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<ShoppingListPermission> ShoppingListPermissions { get; set; }
    }
}