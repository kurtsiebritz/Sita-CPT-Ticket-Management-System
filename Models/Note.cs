using System;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class Note
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Company is required.")]
        public int CompanyId { get; set; }
        public SitaContract Company { get; set; }

        [Required(ErrorMessage = "Content is required.")]
        public string Content { get; set; }

        public string? DocumentPath { get; set; }

        [Required(ErrorMessage = "Created by team member is required.")]
        public int CreatedById { get; set; }
        public SitaTeamMember CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}