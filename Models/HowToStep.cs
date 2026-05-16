using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Models;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class HowToStep
    {
        public int Id { get; set; }

        [Required]
        public int HowToId { get; set; }
        public HowTo HowTo { get; set; }

        [Required]
        public int OrderIndex { get; set; }

        [Required]
        public string? ImagePath { get; set; }

        [Required]
        public string Instructions { get; set; }
    }
}



