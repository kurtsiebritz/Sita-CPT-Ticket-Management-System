using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly SitaCptTicketAppContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        private readonly IMemoryCache _cache;

        public HomeController(SitaCptTicketAppContext context, IConfiguration configuration, ILogger<HomeController> logger, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = DateTime.Now;

            try
            {
                _logger.LogInformation("Fetching dashboard data for {StartDate} to {EndDate}", startDate, endDate);

                // Group by Tickets.Employee directly
                var leaderboardData = await _context.Tickets
                    .Where(t => t.OpenTime >= startDate && t.OpenTime <= endDate && !string.IsNullOrEmpty(t.Employee))
                    .GroupBy(t => t.Employee)
                    .Select(g => new
                    {
                        Employee = g.Key,
                        TicketCount = g.Count()
                    })
                    .OrderByDescending(x => x.TicketCount)
                    .ToListAsync();

                _logger.LogInformation("Leaderboard data: {Data}", JsonSerializer.Serialize(leaderboardData));
                if (!leaderboardData.Any())
                {
                    _logger.LogWarning("No leaderboard data. Tickets: {TicketCount}",
                        await _context.Tickets.CountAsync(t => t.OpenTime >= startDate && t.OpenTime <= endDate));
                }

                var ticketsThisMonth = await _context.Tickets
                    .CountAsync(t => t.OpenTime >= startDate && t.OpenTime <= endDate);
                var mostFrequentLocation = await _context.Tickets
                    .Where(t => t.OpenTime >= startDate && t.OpenTime <= endDate)
                    .GroupBy(t => t.PositionLocation)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key ?? "None")
                    .FirstOrDefaultAsync();
                var startOfLastMonth = startDate.AddMonths(-1);
                var endOfLastMonth = startOfLastMonth.AddDays(DateTime.DaysInMonth(startOfLastMonth.Year, startOfLastMonth.Month) - 1).AddDays(1).AddTicks(-1);
                var lastMonthLeaderboardWinner = await _context.Tickets
                    .Where(t => t.CloseTime != null && t.CloseTime >= startOfLastMonth && t.CloseTime <= endOfLastMonth && !string.IsNullOrEmpty(t.Employee))
                    .GroupBy(t => t.Employee)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync() ?? "No data";
                var daysLeftInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month) - DateTime.Now.Day;

                var concerns = new
                {
                    TicketsThisMonth = ticketsThisMonth.ToString(),
                    MostFrequentLocation = mostFrequentLocation,
                    LastMonthWinner = lastMonthLeaderboardWinner,
                    DaysLeftInMonth = daysLeftInMonth.ToString()
                };

                // Notices and other data remain unchanged
                var today = DateTime.Today;
                var notices = await _context.Notices
                    .Where(n => n.PostedDate >= today.AddDays(-7))
                    .OrderByDescending(n => n.PostedDate)
                    .ToListAsync();
                var noticesWithSize = notices.Select(n =>
                {
                    var filePath = !string.IsNullOrEmpty(n.FilePath)
                        ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", n.FilePath.TrimStart('/'))
                        : null;
                    var fileExists = filePath != null && System.IO.File.Exists(filePath);
                    var fileSize = fileExists ? new FileInfo(filePath).Length : 0L;
                    return new
                    {
                        n.Id,
                        n.Message,
                        n.PostedBy,
                        n.PostedDate,
                        n.FileName,
                        n.FilePath,
                        FileSize = fileSize
                    };
                }).ToList();

                var unresolvedTickets = await _context.Tickets
                    .Where(t => t.CloseTime == null && t.OpenTime.Date == today)
                    .OrderByDescending(t => t.OpenTime)
                    .Select(t => new
                    {
                        t.IncidentNumber,
                        t.Priority,
                        t.PositionLocation,
                        t.OpenTime
                    })
                    .Take(5)
                    .ToListAsync();

                var isAdmin = User.Claims.Any(c => c.Type == "IsAdmin" && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
                ViewBag.IsAdmin = isAdmin;

                ViewBag.LeaderboardData = leaderboardData;
                ViewBag.Concerns = concerns;
                ViewBag.Notices = noticesWithSize;
                ViewBag.UnresolvedTickets = unresolvedTickets;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data for Index action");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostNotice(string message, string postedBy, IFormFile file)
        {
            try
            {
                _logger.LogInformation("User {UserName} attempting to post notice with PostedBy: {PostedBy}", User.Identity.Name, postedBy);

                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogWarning("Invalid notice message: empty or null");
                    TempData["ErrorMessage"] = "Message is required.";
                    return RedirectToAction(nameof(Index));
                }

                postedBy = string.IsNullOrWhiteSpace(postedBy) ? User.Identity.Name ?? "Anonymous" : postedBy.Trim();
                if (postedBy.Length > 256)
                {
                    _logger.LogWarning("PostedBy too long: {Length} characters", postedBy.Length);
                    TempData["ErrorMessage"] = "Posted By cannot exceed 256 characters.";
                    return RedirectToAction(nameof(Index));
                }

                var notice = new Notice
                {
                    Message = message,
                    PostedBy = postedBy,
                    PostedDate = DateTime.UtcNow
                };

                if (file != null && file.Length > 0)
                {
                    var maxFileSize = _configuration.GetValue<long>("MaxFileSizeBytes", 5 * 1024 * 1024);
                    if (file.Length > maxFileSize)
                    {
                        _logger.LogWarning("File too large: {FileName}, Size: {Size} bytes", file.FileName, file.Length);
                        TempData["ErrorMessage"] = $"File size exceeds {maxFileSize / 1024 / 1024}MB limit.";
                        return RedirectToAction(nameof(Index));
                    }

                    var allowedExtensions = _configuration.GetValue<string>("AllowedFileExtensions", ".pdf,.doc,.docx,.jpg,.jpeg,.png").Split(',');
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        _logger.LogWarning("Invalid file extension: {Extension}", extension);
                        TempData["ErrorMessage"] = "Invalid file type. Allowed: " + string.Join(", ", allowedExtensions);
                        return RedirectToAction(nameof(Index));
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    notice.FileName = file.FileName;
                    notice.FilePath = $"/Uploads/{uniqueFileName}";
                    _logger.LogInformation("File uploaded: {FileName}, Path: {FilePath}", notice.FileName, notice.FilePath);
                }
                else
                {
                    _logger.LogInformation("No file uploaded for notice by {UserName}, PostedBy: {PostedBy}", User.Identity.Name, postedBy);
                }

                _context.Notices.Add(notice);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Notice posted successfully by {UserName}, PostedBy: {PostedBy}, ID: {NoticeId}", User.Identity.Name, postedBy, notice.Id);
                TempData["SuccessMessage"] = "Notice posted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting notice by {UserName}, PostedBy: {PostedBy}", User.Identity.Name, postedBy);
                TempData["ErrorMessage"] = "An error occurred while posting the notice.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotice(int id)
        {
            try
            {
                _logger.LogInformation("User {UserName} attempting to delete notice ID: {Id}", User.Identity.Name, id);

                var notice = await _context.Notices.FindAsync(id);
                if (notice == null)
                {
                    _logger.LogWarning("Notice not found: ID {Id}", id);
                    TempData["ErrorMessage"] = "Notice not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrEmpty(notice.FilePath))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", notice.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                        _logger.LogInformation("Deleted file: {FilePath}", filePath);
                    }
                }

                _context.Notices.Remove(notice);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Notice deleted successfully by {UserName}, ID: {Id}", User.Identity.Name, id);
                TempData["DeleteSuccessMessage"] = "Notice deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notice ID {Id} by {UserName}", id, User.Identity.Name);
                TempData["ErrorMessage"] = "An error occurred while deleting the notice.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public IActionResult DownloadFile(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to download file for notice ID: {Id}", id);

                var notice = _context.Notices.Find(id);
                if (notice == null || string.IsNullOrEmpty(notice.FilePath))
                {
                    _logger.LogWarning("Notice or file not found: ID {Id}", id);
                    return NotFound();
                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", notice.FilePath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("File does not exist: {FilePath}", filePath);
                    return NotFound();
                }

                var contentType = Path.GetExtension(notice.FileName).ToLowerInvariant() switch
                {
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                _logger.LogInformation("File downloaded successfully: {FileName}", notice.FileName);
                return File(fileBytes, contentType, notice.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file for notice ID {Id}", id);
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult TeamMemberDetails(string name)
        {
            try
            {
                _logger.LogInformation("Fetching team member details for: {Name}", name);

                if (string.IsNullOrEmpty(name))
                {
                    _logger.LogWarning("Invalid team member name: null or empty");
                    return NotFound();
                }

                var teamMember = _context.SitaTeamMembers.FirstOrDefault(tm => tm.Name == name);
                if (teamMember == null)
                {
                    _logger.LogWarning("SitaTeamMember not found: {Name}", name);
                    return NotFound();
                }

                var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var tickets = _context.Tickets
                    .Where(t => t.Employee == name && t.OpenTime >= startDate)
                    .Select(t => new
                    {
                        t.IncidentNumber,
                        t.OpenTime,
                        t.CloseTime,
                        t.Priority,
                        t.PositionLocation,
                        t.ShortDescription
                    })
                    .ToList();

                _logger.LogInformation("Retrieved {Count} tickets for {Name}", tickets.Count, name);

                var model = new
                {
                    Name = name,
                    Tickets = tickets
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching team member details for {Name}", name);
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ErrorMessage = TempData["ErrorMessage"]?.ToString()
            };
            return View(model);
        }
    }
}