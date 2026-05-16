using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class SitaContract
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CompanyName { get; set; }
    }
}