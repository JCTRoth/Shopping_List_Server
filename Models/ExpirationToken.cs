using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoppingListServer.Models
{
    /// <summary>
    /// A token with a <see cref="Data"/> string and an <see cref="ExpirationTime"/>.
    /// Can be used to store some data that should expire after a certain amount of time,
    /// e.g. a key to share something with someone else.
    /// </summary>
    public class ExpirationToken
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }
        public string Data { get; set; }
        public DateTime ExpirationTime { get; set; }

        public bool IsExpired()
        {
            return ExpirationTime < DateTime.UtcNow;
        }
    }
}
