using System;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class Ticket
    {
        [Key]
        public string IncidentNumber { get; set; }

        public string Priority { get; set; }

        public DateTime OpenTime { get; set; }

        public string Product { get; set; }

        public string Module { get; set; }

        public string AffectedEndUser { get; set; }

        public string ShortDescription { get; set; }

        public string IssueDescription { get; set; }

        public string CallerName { get; set; }

        public string CallerPhone { get; set; }

        public string PositionLocation { get; set; }

        [Required]
        public int ResolutionId { get; set; }

        public Resolution Resolution { get; set; }

        public string ErrorCode { get; set; }

        [Required]
        [StringLength(200)]
        public string Comments { get; set; }

        public string Employee { get; set; }

        public DateTime? CloseTime { get; set; }
    }
}