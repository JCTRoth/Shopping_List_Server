using ShoppingListServer.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace ShoppingListServer.Models
{
    public class ResetPasswordToken
    {
        [Key, Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        // Foreign key for 1:n relationship
        public string UserId { get; set; }
        public virtual User User { get; set; }

        public string Code { get; set; }
        public DateTime ExpirationTime { get; set; }

        /// <summary>
        /// Required for ef core.
        /// </summary>
        public ResetPasswordToken()
        {
        }

        public ResetPasswordToken(User user, string code, DateTime expirationTime)
        {
            User = user;
            Code = code;
            ExpirationTime = expirationTime;
        }
    }
}
