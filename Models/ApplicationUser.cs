using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SitaCptTicketApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(50)]
        public string FirstName { get; set; }

        [StringLength(50)]
        public string Surname { get; set; }

        public bool IsAdmin { get; set; }
    }
}