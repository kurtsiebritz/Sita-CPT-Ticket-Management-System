using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SitaCptTicketApp.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SitaCptTicketAppContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("🌱 Seeding sample data for portfolio...");

                // ====================== Create Admin Role & User ======================
                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                }

                var adminEmail = "admin@sitacpt.com";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FirstName = "Admin",
                        Surname = "User",
                        IsAdmin = true,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@123");

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        logger.LogInformation("✅ Default Admin account created → Email: admin@sitacpt.com | Password: Admin@123");
                    }
                    else
                    {
                        logger.LogWarning("⚠️ Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }

                // ====================== Resolutions ======================
                if (!context.Resolutions.Any())
                {
                    context.Resolutions.AddRange(
                        new Resolution { Name = "Password Reset" },
                        new Resolution { Name = "Software Reinstall" },
                        new Resolution { Name = "Hardware Replacement" },
                        new Resolution { Name = "Network Issue" },
                        new Resolution { Name = "Printer Issue" },
                        new Resolution { Name = "Email Access" },
                        new Resolution { Name = "Application Crash" }
                    );
                    await context.SaveChangesAsync();
                }

                // ====================== Sita Team Members ======================
                if (!context.SitaTeamMembers.Any())
                {
                    context.SitaTeamMembers.AddRange(
                        new SitaTeamMember { Name = "John Doe" },
                        new SitaTeamMember { Name = "Jane Smith" },
                        new SitaTeamMember { Name = "Mike Johnson" },
                        new SitaTeamMember { Name = "Sarah Williams" },
                        new SitaTeamMember { Name = "David Brown" }
                    );
                    await context.SaveChangesAsync();
                }

                // ====================== Sita Contracts ======================
                if (!context.SitaContracts.Any())
                {
                    context.SitaContracts.AddRange(
                        new SitaContract { CompanyName = "SITA Airport Services" },
                        new SitaContract { CompanyName = "Airports Company South Africa" },
                        new SitaContract { CompanyName = "SAA Technical" },
                        new SitaContract { CompanyName = "Vivo Energy" }
                    );
                    await context.SaveChangesAsync();
                }

                // ====================== Sample Tickets ======================
                if (!context.Tickets.Any())
                {
                    var resolutions = context.Resolutions.ToList();
                    var teamMembers = context.SitaTeamMembers.ToList();

                    context.Tickets.AddRange(
                        new Ticket
                        {
                            IncidentNumber = "INC001234",
                            Priority = "High",
                            OpenTime = DateTime.UtcNow.AddDays(-3),
                            Product = "Windows 11",
                            Module = "Login",
                            AffectedEndUser = "User123",
                            ShortDescription = "Cannot log in",
                            IssueDescription = "User cannot log into workstation",
                            CallerName = "Test User",
                            CallerPhone = "10001",
                            PositionLocation = "Gate A1",
                            ResolutionId = resolutions[0].Id,
                            ErrorCode = "ERR001",
                            Comments = "Password reset completed",
                            Employee = teamMembers[0].Name
                        },
                        new Ticket
                        {
                            IncidentNumber = "INC001235",
                            Priority = "Medium",
                            OpenTime = DateTime.UtcNow.AddDays(-2),
                            Product = "Office 365",
                            Module = "Outlook",
                            AffectedEndUser = "User456",
                            ShortDescription = "Email not receiving",
                            IssueDescription = "Outlook not syncing",
                            CallerName = "Test User",
                            CallerPhone = "10002",
                            PositionLocation = "Gate B2",
                            ResolutionId = resolutions[1].Id,
                            ErrorCode = "ERR002",
                            Comments = "Cache cleared",
                            Employee = teamMembers[1].Name
                        },
                        new Ticket
                        {
                            IncidentNumber = "INC001236",
                            Priority = "Low",
                            OpenTime = DateTime.UtcNow.AddDays(-1),
                            Product = "Printer",
                            Module = "HP LaserJet",
                            AffectedEndUser = "User789",
                            ShortDescription = "Printer offline",
                            IssueDescription = "Printer not responding",
                            CallerName = "Test User",
                            CallerPhone = "10003",
                            PositionLocation = "Gate C1",
                            ResolutionId = resolutions[4].Id,
                            ErrorCode = "ERR003",
                            Comments = "Reinstalled driver",
                            Employee = teamMembers[2].Name
                        }
                    );
                    await context.SaveChangesAsync();
                }

                // ====================== HowTos + Steps ======================
                if (!context.HowTos.Any())
                {
                    var contracts = context.SitaContracts.ToList();
                    var teamMembers = context.SitaTeamMembers.ToList();

                    var howTo = new HowTo
                    {
                        Title = "How to Reset Password on Windows 11",
                        CompanyId = contracts[0].Id,
                        CreatedById = teamMembers[0].Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-5)
                    };

                    context.HowTos.Add(howTo);
                    await context.SaveChangesAsync();

                    context.HowToSteps.AddRange(
                        new HowToStep { HowToId = howTo.Id, OrderIndex = 1, Instructions = "Press Ctrl + Alt + Del and select Change a password", ImagePath = "" },
                        new HowToStep { HowToId = howTo.Id, OrderIndex = 2, Instructions = "Enter your current password and new password twice", ImagePath = "" }
                    );
                    await context.SaveChangesAsync();
                }

                // ====================== Notices ======================
                if (!context.Notices.Any())
                {
                    context.Notices.AddRange(
                        new Notice
                        {
                            Message = "System maintenance scheduled for this weekend (Saturday 02:00-04:00)",
                            PostedBy = "Admin",
                            PostedDate = DateTime.UtcNow.AddDays(-1),
                            FileName = "",
                            FilePath = ""
                        },
                        new Notice
                        {
                            Message = "New IT Security Policy - Please review",
                            PostedBy = "Jane Smith",
                            PostedDate = DateTime.UtcNow.AddDays(-3),
                            FileName = "",
                            FilePath = ""
                        }
                    );
                    await context.SaveChangesAsync();
                }

                logger.LogInformation("✅ Sample data seeded successfully! Application now looks fully populated.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error while seeding sample data");
            }
        }
    }
}