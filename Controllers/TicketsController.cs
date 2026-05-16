using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using SitaCptTicketApp.Services;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SitaCptTicketApp.Controllers
{
    public class TicketsController : Controller
    {
        private readonly SitaCptTicketAppContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TicketsController> _logger;
        private readonly IncidentParserService _parserService;

        public TicketsController(UserManager<ApplicationUser> userManager, SitaCptTicketAppContext context, IncidentParserService parserService, ILogger<TicketsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager;
            _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("TicketsController: Initialized at {Timestamp}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        // Index action
        public async Task<IActionResult> Index(int? month, string incidentNumber)
        {
            _logger.LogInformation("Index: Entered at {Timestamp}; Month={Month}; IncidentNumber={IncidentNumber}; Url={Url}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), month, incidentNumber, Request.Path + Request.QueryString);

            var ticketsQuery = _context.Tickets
                .Include(t => t.Resolution)
                .AsQueryable();

            if (month.HasValue)
            {
                _logger.LogInformation("Index: Applying month filter: Month={Month}, Year={Year}", month.Value, DateTime.Now.Year);
                ticketsQuery = ticketsQuery.Where(t => t.OpenTime.Month == month.Value && t.OpenTime.Year == DateTime.Now.Year);
            }
            else
            {
                _logger.LogInformation("Index: No month filter applied; Retrieving all tickets");
            }

            if (!string.IsNullOrEmpty(incidentNumber))
            {
                incidentNumber = incidentNumber.Trim();
                _logger.LogInformation("Index: Applying incidentNumber filter: {IncidentNumber}", incidentNumber);
                ticketsQuery = ticketsQuery.Where(t => t.IncidentNumber.ToLower().Contains(incidentNumber.ToLower()));
            }

            var tickets = await ticketsQuery.ToListAsync();
            _logger.LogInformation("Index: Tickets retrieved, count={Count}; TicketIds=[{TicketIds}]",
                tickets.Count, string.Join(",", tickets.Select(t => t.IncidentNumber)));

            ViewBag.Month = month;
            ViewBag.IncidentNumber = incidentNumber;

            return View(tickets);
        }

        // ExportToCsv action
        [Authorize]
        public async Task<IActionResult> ExportToCsv(int? month, string incidentNumber)
        {
            _logger.LogInformation("ExportToCsv: Entered at {Timestamp}; Month={Month}; IncidentNumber={IncidentNumber}; Url={Url}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), month, incidentNumber, Request.Path + Request.QueryString);

            var ticketsQuery = _context.Tickets
                .Include(t => t.Resolution)
                .AsQueryable();

            if (month.HasValue)
            {
                _logger.LogInformation("ExportToCsv: Applying month filter: Month={Month}, Year={Year}", month.Value, DateTime.Now.Year);
                ticketsQuery = ticketsQuery.Where(t => t.OpenTime.Month == month.Value && t.OpenTime.Year == DateTime.Now.Year);
            }
            else
            {
                _logger.LogInformation("ExportToCsv: No month filter applied; Retrieving all tickets");
            }

            if (!string.IsNullOrEmpty(incidentNumber))
            {
                incidentNumber = incidentNumber.Trim();
                _logger.LogInformation("ExportToCsv: Applying incidentNumber filter: {IncidentNumber}", incidentNumber);
                ticketsQuery = ticketsQuery.Where(t => t.IncidentNumber.ToLower().Contains(incidentNumber.ToLower()));
            }

            var tickets = await ticketsQuery.ToListAsync();
            _logger.LogInformation("ExportToCsv: Tickets retrieved, count={Count}; TicketIds=[{TicketIds}]",
                tickets.Count, string.Join(",", tickets.Select(t => t.IncidentNumber)));

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("IncidentNumber,OpenTime,CloseTime,Priority,Product,Module,AffectedEndUser,CallerName,CallerPhone,ShortDescription,PositionLocation,IssueDescription,Resolution,Employee,Comments,ErrorCode");

            foreach (var ticket in tickets)
            {
                var shortDescription = $"\"{ticket.ShortDescription?.Replace("\"", "\"\"") ?? ""}\"";
                var issueDescription = $"\"{ticket.IssueDescription?.Replace("\"", "\"\"") ?? ""}\"";
                var comments = $"\"{ticket.Comments?.Replace("\"", "\"\"") ?? ""}\"";
                var resolutionName = $"\"{ticket.Resolution?.Name?.Replace("\"", "\"\"") ?? ""}\"";
                csvBuilder.AppendLine($"{ticket.IncidentNumber},{ticket.OpenTime:yyyy-MM-dd HH:mm:ss},{ticket.CloseTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""},{ticket.Priority ?? ""},{ticket.Product ?? ""},{ticket.Module ?? ""},{ticket.AffectedEndUser ?? ""},{ticket.CallerName ?? ""},{ticket.CallerPhone ?? ""},{shortDescription},{ticket.PositionLocation ?? ""},{issueDescription},{resolutionName},{ticket.Employee ?? ""},{comments},{ticket.ErrorCode ?? ""}");
            }

            var csvBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            var fileName = $"Tickets_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            _logger.LogInformation("ExportToCsv: Generated CSV file {FileName} with {Count} tickets at {Timestamp}",
                fileName, tickets.Count, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            return File(csvBytes, "text/csv", fileName);
        }

        // AddIncident action
        public async Task<IActionResult> AddIncident()
        {
            _logger.LogInformation("AddIncident: Entered at {Timestamp}; User={UserName}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), User.Identity?.Name);

            var resolutions = await _context.Resolutions.OrderBy(r => r.Name).ToListAsync();
            ViewBag.Resolutions = new SelectList(await _context.Resolutions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name");
            var resolutionCount = resolutions.Count;
            _logger.LogInformation("AddIncident: Loaded resolutions, count={Count}", resolutionCount);

            var userEmail = User.Identity?.Name;
            string loggedInUserFullName = string.Empty;
            if (!string.IsNullOrEmpty(userEmail))
            {
                var user = await _userManager.Users
                    .Where(u => u.Email.ToLower() == userEmail.ToLower())
                    .Select(u => new { u.FirstName, u.Surname })
                    .FirstOrDefaultAsync();
                ViewBag.LoggedInUserFullName = user != null && !string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.Surname)
                    ? $"{user.FirstName} {user.Surname}"
                    : string.Empty;
                if (string.IsNullOrEmpty(ViewBag.LoggedInUserFullName))
                {
                    _logger.LogWarning("AddIncident: No user found or missing name fields for email {Email}", userEmail);
                }
                else
                {
                    _logger.LogInformation("AddIncident: User full name set to {FullName}", loggedInUserFullName);
                }
            }
            else
            {
                ViewBag.LoggedInUserFullName = string.Empty;
                _logger.LogWarning("AddIncident: No logged-in user found");
            }

            return View(new Ticket());
        }

        // ParseIncident action
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ParseIncident(string IncidentText)
        {
            _logger.LogInformation("ParseIncident: Entered at {Timestamp}; IncidentTextLength={Length}; User={UserName}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), IncidentText?.Length ?? 0, User.Identity?.Name);

            var (ticket, error) = await _parserService.ParseIncidentText(IncidentText);
            var resolutions = await _context.Resolutions.OrderBy(r => r.Name).ToListAsync();
            ViewBag.IncidentText = IncidentText;
            ViewBag.Resolutions = new SelectList(await _context.Resolutions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name");
            var resolutionCount = resolutions.Count;
            _logger.LogInformation("ParseIncident: Loaded resolutions, count={Count}", resolutionCount);
            if (ticket == null)
            {
                _logger.LogWarning("ParseIncident: Parsing failed; Error={Error}", error);
                TempData["ErrorMessage"] = error ?? "Failed to parse incident text.";
                return View("AddIncident", new Ticket());
            }

            // Set Employee field to the full name of the currently signed-in user
            if (User.Identity?.IsAuthenticated == true)
            {
                var userEmail = User.Identity.Name;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    var user = await _userManager.Users
                        .Where(u => u.Email.ToLower() == userEmail.ToLower())
                        .Select(u => new { u.FirstName, u.Surname })
                        .FirstOrDefaultAsync();
                    if (user != null && !string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.Surname))
                    {
                        ticket.Employee = $"{user.FirstName} {user.Surname}";
                        ViewBag.LoggedInUserFullName = ticket.Employee; // Set for view consistency
                        //_logger.LogInformation("ParseIncident: Set Employee={Employee} and ViewBag.LoggedInUserFullName={FullName} for user email {Email}", ticket.Employee, ViewBag.LoggedInUserFullName, userEmail);
                    }
                    else
                    {
                        _logger.LogWarning("ParseIncident: No user found or missing name fields for email {Email}; Employee field not set", userEmail);
                        ticket.Employee = null;
                        ViewBag.LoggedInUserFullName = string.Empty;
                    }
                }
                else
                {
                    _logger.LogWarning("ParseIncident: User.Identity.Name is null or empty; Employee field not set");
                    ticket.Employee = null;
                    ViewBag.LoggedInUserFullName = string.Empty;
                }
            }
            else
            {
                _logger.LogWarning("ParseIncident: User is not authenticated; Employee field not set");
                ticket.Employee = null;
                ViewBag.LoggedInUserFullName = string.Empty;
            }

            _logger.LogInformation("ParseIncident: Parsed ticket; IncidentNumber={IncidentNumber}, Priority={Priority}, OpenTime={OpenTime}, Product={Product}, Module={Module}, AffectedEndUser={AffectedEndUser}, CallerName={CallerName}, CallerPhone={CallerPhone}, ShortDescription={ShortDescription}, PositionLocation={PositionLocation}, IssueDescription={IssueDescription}, ResolutionId={ResolutionId}, Comments={Comments}, ErrorCode={ErrorCode}, Employee={Employee}",
                ticket.IncidentNumber, ticket.Priority, ticket.OpenTime.ToString("yyyy-MM-dd HH:mm:ss"), ticket.Product, ticket.Module, ticket.AffectedEndUser, ticket.CallerName, ticket.CallerPhone, ticket.ShortDescription, ticket.PositionLocation, ticket.IssueDescription, ticket.ResolutionId, ticket.Comments, ticket.ErrorCode, ticket.Employee);

            return View("AddIncident", ticket);
        }

        // SaveIncident action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveIncident(Ticket ticket, string submitAction, string customComment)
        {
            _logger.LogInformation("SaveIncident: Entered at {Timestamp}; IncidentNumber={IncidentNumber}; SubmitAction={SubmitAction}; CustomComment={CustomComment}; ResolutionId={ResolutionId}; OpenTime={OpenTime}; CloseTime={CloseTime}; Employee={Employee}; Comments={Comments}; ErrorCode={ErrorCode}; User={UserName}; Url={Url}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), ticket?.IncidentNumber, submitAction, customComment, ticket?.ResolutionId, ticket?.OpenTime.ToString("yyyy-MM-dd HH:mm:ss"), ticket?.CloseTime?.ToString("yyyy-MM-dd HH:mm:ss"), ticket?.Employee, ticket?.Comments, ticket?.ErrorCode, User.Identity?.Name, Request.Path + Request.QueryString);

            _logger.LogInformation("SaveIncident: Raw form data: {FormData}", string.Join("; ", Request.Form.Select(f => $"{f.Key}={f.Value}")));

            // Log initial ModelState errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("SaveIncident: Initial ModelState invalid; Errors={Errors}", System.Text.Json.JsonSerializer.Serialize(errors));
            }

            // Remove erroneous Resolution validation
            if (ModelState.ContainsKey("Resolution"))
            {
                _logger.LogInformation("SaveIncident: Clearing erroneous 'Resolution' ModelState entry");
                ModelState.Remove("Resolution");
            }

            // Validate ResolutionId
            if (ticket.ResolutionId <= 0)
            {
                _logger.LogWarning("SaveIncident: Invalid ResolutionId={ResolutionId}; Adding validation error", ticket.ResolutionId);
                ModelState.AddModelError("ResolutionId", "The Resolution field is required.");
            }
            else
            {
                var resolutionExists = await _context.Resolutions.AnyAsync(r => r.Id == ticket.ResolutionId);
                _logger.LogInformation("SaveIncident: ResolutionId={ResolutionId} from form; Exists in DB={ResolutionExists}", ticket.ResolutionId, resolutionExists);
                if (!resolutionExists)
                {
                    _logger.LogWarning("SaveIncident: ResolutionId={ResolutionId} does not exist in DB; Adding validation error", ticket.ResolutionId);
                    ModelState.AddModelError("ResolutionId", "The selected resolution does not exist.");
                }
            }

            // Handle Comments and customComment
            if (ticket.Comments == "Other")
            {
                if (string.IsNullOrWhiteSpace(customComment))
                {
                    _logger.LogWarning("SaveIncident: Other selected but customComment is empty; Adding validation error");
                    ModelState.AddModelError("customComment", "Custom comment is required when 'Other' is selected.");
                }
                else
                {
                    ticket.Comments = customComment;
                    _logger.LogInformation("SaveIncident: Set Comments to customComment={CustomComment}", customComment);
                }
            }
            else
            {
                if (ModelState.ContainsKey("customComment"))
                {
                    _logger.LogInformation("SaveIncident: Clearing customComment ModelState as Comments is not 'Other'");
                    ModelState.Remove("customComment");
                }
            }

            if (string.IsNullOrWhiteSpace(ticket.Comments))
            {
                _logger.LogWarning("SaveIncident: Comments is empty; Adding validation error");
                ModelState.AddModelError("Comments", "The Comments field is required.");
            }

            // Check for duplicate ticket
            var existingTicket = await _context.Tickets.AnyAsync(t => t.IncidentNumber == ticket.IncidentNumber);
            if (existingTicket)
            {
                _logger.LogWarning("SaveIncident: Duplicate ticket found for IncidentNumber={IncidentNumber}", ticket.IncidentNumber);
                ModelState.AddModelError("IncidentNumber", $"Incident Number {ticket.IncidentNumber} already exists.");
            }

            // Log final ModelState errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("SaveIncident: Final ModelState invalid; Errors={Errors}", System.Text.Json.JsonSerializer.Serialize(errors));

                if (Request.Form.ContainsKey("OpenTime"))
                {
                    var openTimeStr = Request.Form["OpenTime"].ToString();
                    if (DateTime.TryParse(openTimeStr, out var openTime))
                    {
                        ticket.OpenTime = openTime;
                        _logger.LogInformation("SaveIncident: Restored OpenTime from form; OpenTime={OpenTime}", ticket.OpenTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                }

                var resolutions = await _context.Resolutions.OrderBy(r => r.Name).ToListAsync();
                var resolutionCount = resolutions.Count;
                ViewBag.Resolutions = new SelectList(resolutions, "Id", "Name", ticket.ResolutionId);
                _logger.LogInformation("SaveIncident: Resolutions loaded, count={Count}; Selected={Selected}", resolutionCount, ticket.ResolutionId);
                ViewBag.IncidentText = Request.Form["IncidentText"].ToString();
                return View("AddIncident", ticket);
            }

            try
            {
                _logger.LogInformation("SaveIncident: Attempting to save ticket; IncidentNumber={IncidentNumber}, ResolutionId={ResolutionId}, Comments={Comments}, Employee={Employee}",
                    ticket.IncidentNumber, ticket.ResolutionId, ticket.Comments, ticket.Employee);
                await _parserService.SaveTicketAsync(ticket);
                _logger.LogInformation("SaveIncident: Ticket saved; IncidentNumber={IncidentNumber} at {Timestamp}",
                    ticket.IncidentNumber, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                TempData["SuccessMessage"] = $"Ticket {ticket.IncidentNumber} saved successfully.";

                if (submitAction == "SaveAndAddAnother")
                {
                    _logger.LogInformation("SaveIncident: SaveAndAddAnother; Redirecting to AddIncident");
                    return RedirectToAction("AddIncident");
                }
                _logger.LogInformation("SaveIncident: Save; Redirecting to Index");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveIncident: Error saving ticket; IncidentNumber={IncidentNumber}, Message={Message}, InnerException={InnerException}",
                    ticket.IncidentNumber, ex.Message, ex.InnerException?.Message);
                TempData["ErrorMessage"] = "An error occurred while saving the ticket: " + (ex.InnerException?.Message ?? ex.Message);
                ViewBag.Resolutions = new SelectList(await _context.Resolutions.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", ticket.ResolutionId);
                ViewBag.IncidentText = Request.Form["IncidentText"].ToString();
                return View("AddIncident", ticket);
            }
        }

        // GetCommentsByResolution action
        [HttpGet]
        public async Task<IActionResult> GetCommentsByResolution(int resolutionId)
        {
            _logger.LogInformation("GetCommentsByResolution: Entered at {Timestamp}; ResolutionId={ResolutionId}; Url={Url}; User={UserName}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), resolutionId, Request.Path + Request.QueryString, User.Identity?.Name);

            var resolutionExists = await _context.Resolutions.AnyAsync(r => r.Id == resolutionId);
            _logger.LogInformation("GetCommentsByResolution: Resolution exists for Id={ResolutionId}: {ResolutionExists}", resolutionId, resolutionExists);
            if (!resolutionExists)
            {
                _logger.LogWarning("GetCommentsByResolution: ResolutionId={ResolutionId} not found", resolutionId);
                return Json(new { error = "Resolution not found" });
            }

            var comments = await _context.Comments
                .Where(c => c.ResolutionId == resolutionId)
                .Select(c => new { comment = c.Name, errorCode = c.ErrorCode })
                .OrderBy(c => c.comment)
                .ToListAsync();

            _logger.LogInformation("GetCommentsByResolution: Comments retrieved, count={Count}; Comments=[{Comments}]",
                comments.Count, string.Join("; ", comments.Select(c => $"Name={c.comment},ErrorCode={c.errorCode}")));
            return Json(comments);
        }
    }
}