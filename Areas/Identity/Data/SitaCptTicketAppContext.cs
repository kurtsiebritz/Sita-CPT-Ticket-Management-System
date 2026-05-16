using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Models;

namespace SitaCptTicketApp.Data
{
    public class SitaCptTicketAppContext : IdentityDbContext<ApplicationUser>
    {
        public SitaCptTicketAppContext(DbContextOptions<SitaCptTicketAppContext> options)
            : base(options)
        {
        }

        public DbSet<LoginHistory> LoginHistory { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<SitaTeamMember> SitaTeamMembers { get; set; }
        public DbSet<HowTo> HowTos { get; set; }
        public DbSet<HowToStep> HowToSteps { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<SitaContract> SitaContracts { get; set; }
        public DbSet<Resolution> Resolutions { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Notice> Notices { get; set; }
        public DbSet<ContactList> ContactLists { get; set; }
        public DbSet<Contact> Contacts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure SitaTeamMember entity
            builder.Entity<SitaTeamMember>()
                .ToTable("SitaTeamMembers")
                .HasKey(t => t.Id);

            builder.Entity<SitaTeamMember>()
                .Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(50);

            // Configure HowTo entity
            builder.Entity<HowTo>()
                .ToTable("HowTos");

            builder.Entity<HowTo>()
                .HasKey(h => h.Id);

            builder.Entity<HowTo>()
                .Property(h => h.Title)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<HowTo>()
                .HasOne(h => h.Company)
                .WithMany()
                .HasForeignKey(h => h.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HowTo>()
                .HasOne(h => h.CreatedBy)
                .WithMany()
                .HasForeignKey(h => h.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure HowToStep entity
            builder.Entity<HowToStep>()
                .ToTable("HowToSteps");

            builder.Entity<HowToStep>()
                .HasKey(s => s.Id);

            builder.Entity<HowToStep>()
                .Property(s => s.Instructions)
                .IsRequired();

            builder.Entity<HowToStep>()
                .Property(s => s.ImagePath)
                .HasMaxLength(500);

            builder.Entity<HowToStep>()
                .HasOne(s => s.HowTo)
                .WithMany(h => h.Steps)
                .HasForeignKey(s => s.HowToId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Note entity
            builder.Entity<Note>()
                .ToTable("Notes");

            builder.Entity<Note>()
                .HasOne(n => n.Company)
                .WithMany()
                .HasForeignKey(n => n.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Note>()
                .HasOne(n => n.CreatedBy)
                .WithMany()
                .HasForeignKey(n => n.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure existing entities
            builder.Entity<Ticket>()
                .ToTable("Tickets");

            builder.Entity<Ticket>()
                .HasKey(t => t.IncidentNumber);

            builder.Entity<Ticket>()
                .Property(t => t.Comments)
                .IsRequired()
                .HasMaxLength(200);

            builder.Entity<Resolution>()
                .HasKey(r => r.Id);

            builder.Entity<Resolution>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<Comment>()
                .HasKey(c => c.Id);

            builder.Entity<Comment>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Entity<Comment>()
                .Property(c => c.ErrorCode)
                .HasMaxLength(50);

            builder.Entity<Notice>()
                .HasKey(n => n.Id);

            builder.Entity<Notice>()
                .Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(500);

            builder.Entity<Notice>()
                .Property(n => n.PostedBy)
                .HasMaxLength(100);

            builder.Entity<Notice>()
                .Property(n => n.FileName)
                .HasMaxLength(255);

            builder.Entity<Notice>()
                .Property(n => n.FilePath)
                .HasMaxLength(500);

            builder.Entity<ContactList>()
                .HasKey(cl => cl.Id);

            builder.Entity<ContactList>()
                .Property(cl => cl.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<Contact>()
                .HasKey(c => c.Id);

            builder.Entity<Contact>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<Contact>()
                .Property(c => c.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            // Configure relationships
            builder.Entity<Ticket>()
                .HasOne(t => t.Resolution)
                .WithMany()
                .HasForeignKey(t => t.ResolutionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Comment>()
                .HasOne(c => c.Resolution)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ResolutionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Contact>()
                .HasOne(c => c.ContactList)
                .WithMany(cl => cl.Contacts)
                .HasForeignKey(c => c.ContactListId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed initial data for ContactList and Contacts
            builder.Entity<ContactList>().HasData(
                new ContactList { Id = 1, Name = "Gates" }
            );

            builder.Entity<Contact>().HasData(
                new Contact { Id = 1, ContactListId = 1, Name = "Gate A1", PhoneNumber = "+1-555-123-4567" },
                new Contact { Id = 2, ContactListId = 1, Name = "Gate A2", PhoneNumber = "+1-555-123-4568" },
                new Contact { Id = 3, ContactListId = 1, Name = "Gate B1", PhoneNumber = "+1-555-123-4569" },
                new Contact { Id = 4, ContactListId = 1, Name = "Gate B2", PhoneNumber = "+1-555-123-4570" },
                new Contact { Id = 5, ContactListId = 1, Name = "Gate C1", PhoneNumber = "+1-555-123-4571" },
                new Contact { Id = 6, ContactListId = 1, Name = "Gate C2", PhoneNumber = "+1-555-123-4572" }
            );
        }
    }
}