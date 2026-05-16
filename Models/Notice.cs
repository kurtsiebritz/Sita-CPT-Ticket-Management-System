using System;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class Notice
    {
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        public DateTime PostedDate { get; set; }

        [StringLength(100)]
        public string PostedBy { get; set; }

        [StringLength(255)]
        public string? FileName { get; set; }

        [StringLength(500)]
        public string? FilePath { get; set; }
    }
}