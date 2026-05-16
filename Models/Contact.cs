namespace SitaCptTicketApp.Models
{
    public class Contact
    {
        public int Id { get; set; }
        public int ContactListId { get; set; } // Foreign key
        public ContactList ContactList { get; set; }
        public string Name { get; set; } // e.g., "Gate A1"
        public string PhoneNumber { get; set; } // e.g., "+1-555-123-4567"
    }
}