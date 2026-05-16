using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SitaCptTicketApp.Models
{
    public class LoginHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("ApplicationUser")]
        public string UserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; }

        [Required]
        public DateTime LoginTime { get; set; }

        [MaxLength(45)]
        public string IpAddress { get; set; }

        public ApplicationUser User { get; set; }
    }
}