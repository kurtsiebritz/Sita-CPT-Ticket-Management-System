using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class SitaTeamMember
    {
        [Key]
        public int Id { get; set; }

        [StringLength(50)]
        public string Name { get; set; }
    }
}
