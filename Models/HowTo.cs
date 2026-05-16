using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class HowTo
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        public int CompanyId { get; set; }
        public SitaContract Company { get; set; }

        [Required]
        public int CreatedById { get; set; }
        public SitaTeamMember CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public List<HowToStep> Steps { get; set; } = new List<HowToStep>();
    }
}