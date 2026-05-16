using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(50)]
        public string ErrorCode { get; set; }

        [Required]
        public int ResolutionId { get; set; }

        public Resolution? Resolution { get; set; }
    }
}