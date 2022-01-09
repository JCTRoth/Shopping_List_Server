using ShoppingListServer.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Models
{
    // Token used for verifying an email address for a user.
    public class EMailVerificationToken
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }
        
        // Foreign key for 1:n relationship
        public string UserId {get; set;}
        public virtual User User { get; set; }

        public string UrlCode { get; set; }
        public DateTime ExpirationTime { get; set; }

        public EMailVerificationToken()
        {
        }

        public EMailVerificationToken(string urlCode, DateTime exiprationTime)
        {
            this.UrlCode = urlCode;
            this.ExpirationTime = exiprationTime;
        }
    }
}
