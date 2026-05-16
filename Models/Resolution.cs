using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class Resolution
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public List<Comment> Comments { get; set; } = new();
    }
}