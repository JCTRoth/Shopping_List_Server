using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ShoppingListServer.Entities;

namespace ShoppingListServer.Models
{
    public class FcmToken
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key for 1:n relationship
        public string UserId { get; set; }
        public virtual User User { get; set; }

        public string Token { get; set; }
	}
}

