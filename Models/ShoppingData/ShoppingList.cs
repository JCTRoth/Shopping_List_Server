using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Newtonsoft.Json;
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
        public List<GenericProduct> ProductList { get; set; }

        [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public virtual List<ShoppingListPermission> ShoppingListPermissions { get; set; }
    }
}