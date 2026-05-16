namespace SitaCptTicketApp.Models
{
    public class ContactList
    {
        public int Id { get; set; }
        public string Name { get; set; } // e.g., "Gates", "SAA Staff"
        public List<Contact> Contacts { get; set; } = new List<Contact>();
    }
}