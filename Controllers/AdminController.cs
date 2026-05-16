using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SitaCptTicketApp.Controllers
{
    public class LogAuthorizationFilter : IAuthorizationFilter
    {
        private readonly ILogger<LogAuthorizationFilter> _logger;

        public LogAuthorizationFilter(ILogger<LogAuthorizationFilter> logger)
        {
            _logger = logger;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userName = context.HttpContext.User.Identity?.Name ?? "Anonymous";
            var path = context.HttpContext.Request.Path;
            _logger.LogInformation("Authorization attempt for path: {Path}, user: {UserName} at {Timestamp}", path, userName, DateTime.UtcNow);

            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var claims = context.HttpContext.User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                _logger.LogDebug("Claims for {UserName} at {Path}: {Claims}", userName, path, string.Join("; ", claims));
                var isAdminClaim = claims.FirstOrDefault(c => c.Contains("IsAdmin"));
                _logger.LogDebug("IsAdmin claim for {UserName}: {IsAdminClaim}", userName, isAdminClaim ?? "null");
                var roleClaim = claims.FirstOrDefault(c => c.Contains("role"));
                _logger.LogDebug("Role claim for {UserName}: {RoleClaim}", userName, roleClaim ?? "null");
            }
            else
            {
                _logger.LogWarning("User is not authenticated for {Path} at {Timestamp}", path, DateTime.UtcNow);
            }

            if (context.Result != null)
            {
                _logger.LogWarning("Authorization failed for {Path}, user: {UserName}. Result: {ResultType} at {Timestamp}",
                    path, userName, context.Result?.GetType().Name, DateTime.UtcNow);
            }
        }
    }

    [Authorize(Policy = "RequireAdmin")]
    [ServiceFilter(typeof(LogAuthorizationFilter))]
    public class AdminController : Controller
    {
        private readonly SitaCptTicketAppContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            SitaCptTicketAppContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("AdminController instantiated for request at {Timestamp}", DateTime.UtcNow);
        }

        [HttpGet]
        public async Task<IActionResult> Index(string emailFilter = null, string sortOrder = null)
        {
            _logger.LogInformation("GET /Admin/Index called for user: {UserName}; EmailFilter={EmailFilter}, SortOrder={SortOrder} at {Timestamp}",
                User.Identity?.Name, emailFilter ?? "none", sortOrder ?? "none", DateTime.UtcNow);

            var loginHistoryQuery = _context.LoginHistory
                .Include(l => l.User)
                .Select(l => new LoginHistoryViewModel
                {
                    UserId = l.UserId,
                    Email = l.Email,
                    FullName = l.User != null ? $"{l.User.FirstName} {l.User.Surname}" : l.Email,
                    LoginTime = l.LoginTime,
                    IpAddress = l.IpAddress
                });

            if (!string.IsNullOrEmpty(emailFilter))
            {
                loginHistoryQuery = loginHistoryQuery.Where(l => l.Email.Contains(emailFilter));
                _logger.LogInformation("Admin/Index: Applied email filter={EmailFilter} at {Timestamp}", emailFilter, DateTime.UtcNow);
            }

            loginHistoryQuery = sortOrder switch
            {
                "email_desc" => loginHistoryQuery.OrderByDescending(l => l.Email),
                "email" => loginHistoryQuery.OrderBy(l => l.Email),
                "time_desc" => loginHistoryQuery.OrderByDescending(l => l.LoginTime),
                "time" => loginHistoryQuery.OrderBy(l => l.LoginTime),
                "ip_desc" => loginHistoryQuery.OrderByDescending(l => l.IpAddress),
                "ip" => loginHistoryQuery.OrderBy(l => l.IpAddress),
                _ => loginHistoryQuery.OrderByDescending(l => l.LoginTime)
            };
            _logger.LogInformation("Admin/Index: Applied sort order={SortOrder} at {Timestamp}", sortOrder ?? "default (time_desc)", DateTime.UtcNow);

            var loginHistory = await loginHistoryQuery.ToListAsync();
            _logger.LogInformation("Admin/Index: Retrieved {Count} login history records at {Timestamp}", loginHistory.Count, DateTime.UtcNow);

            ViewBag.EmailFilter = emailFilter;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.LoginHistory = loginHistory;

            return View("Index");
        }

        [HttpGet]
        public IActionResult AdminDashboard()
        {
            _logger.LogInformation("GET /Admin/AdminDashboard called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in AdminDashboard for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var isAdminClaim = claims.FirstOrDefault(c => c.Contains("IsAdmin"));
            if (isAdminClaim == null || !isAdminClaim.Contains("true"))
            {
                _logger.LogWarning("IsAdmin claim missing or not 'true' for {UserName}: {IsAdminClaim} at {Timestamp}",
                    User.Identity?.Name, isAdminClaim ?? "null", DateTime.UtcNow);
            }
            return View("Index");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult DebugAccess()
        {
            _logger.LogInformation("GET /Admin/DebugAccess called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            _logger.LogDebug("Claims in DebugAccess for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            return Json(new { User = User.Identity?.Name, Claims = claims, IsAuthenticated = User.Identity?.IsAuthenticated });
        }

        [HttpGet]
        public IActionResult GetClaims()
        {
            _logger.LogInformation("GET /Admin/GetClaims called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            _logger.LogDebug("Returning claims for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            return Json(claims);
        }

        [HttpGet]
        public async Task<IActionResult> EditComments()
        {
            _logger.LogInformation("GET /Admin/EditComments called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditComments for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var comments = await _context.Comments
                .Include(c => c.Resolution)
                .OrderBy(c => c.Resolution != null ? c.Resolution.Name : "No Resolution")
                .ThenBy(c => c.Name)
                .ToListAsync();
            _logger.LogInformation("Retrieved {Count} comments for EditComments at {Timestamp}", comments.Count, DateTime.UtcNow);
            return View(comments);
        }

        [HttpGet]
        public async Task<IActionResult> CreateComment(int resolutionId)
        {
            _logger.LogInformation("GET /Admin/CreateComment called for resolution ID: {ResolutionId}, user: {UserName} at {Timestamp}", resolutionId, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in CreateComment for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var resolution = await _context.Resolutions.FindAsync(resolutionId);
            if (resolution == null)
            {
                _logger.LogWarning("CreateComment GET: ResolutionId={ResolutionId} not found for user: {UserName} at {Timestamp}", resolutionId, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Resolution not found.";
                return RedirectToAction("EditComments");
            }

            var model = new Comment { ResolutionId = resolutionId };
            ViewBag.ResolutionName = resolution.Name ?? "None";
            //_logger.LogInformation("CreateComment GET: ResolutionId={ResolutionId}, ResolutionName={ResolutionName} at {Timestamp}", resolutionId, ViewBag.ResolutionName, DateTime.UtcNow);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComment([Bind("Name,ErrorCode,ResolutionId")] Comment comment)
        {
            _logger.LogInformation("POST /Admin/CreateComment called at {Timestamp}; Name={Name}; ResolutionId={ResolutionId}; ErrorCode={ErrorCode}; User={UserName}; Url={Url}",
                DateTime.UtcNow, comment.Name ?? "null", comment.ResolutionId, comment.ErrorCode ?? "null", User.Identity?.Name, Request.Path + Request.QueryString);

            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in CreateComment POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            _logger.LogInformation("CreateComment: Raw form data: {FormData}", string.Join("; ", Request.Form.Select(f => $"{f.Key}={f.Value}")));

            // Log initial ModelState errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("CreateComment: Initial ModelState invalid; Errors={Errors} at {Timestamp}", System.Text.Json.JsonSerializer.Serialize(errors), DateTime.UtcNow);
            }

            // Remove erroneous Resolution validation
            if (ModelState.ContainsKey("Resolution"))
            {
                _logger.LogInformation("CreateComment: Clearing erroneous 'Resolution' ModelState entry at {Timestamp}", DateTime.UtcNow);
                ModelState.Remove("Resolution");
            }

            // Validate ResolutionId
            if (comment.ResolutionId <= 0)
            {
                _logger.LogWarning("CreateComment: Invalid ResolutionId={ResolutionId}; Adding validation error at {Timestamp}", comment.ResolutionId, DateTime.UtcNow);
                ModelState.AddModelError("ResolutionId", "The Resolution field is required.");
            }
            else
            {
                var resolutionExists = await _context.Resolutions.AnyAsync(r => r.Id == comment.ResolutionId);
                _logger.LogInformation("CreateComment: ResolutionId={ResolutionId} from form; Exists in DB={ResolutionExists} at {Timestamp}", comment.ResolutionId, resolutionExists, DateTime.UtcNow);
                if (!resolutionExists)
                {
                    _logger.LogWarning("CreateComment: ResolutionId={ResolutionId} does not exist in DB; Adding validation error at {Timestamp}", comment.ResolutionId, DateTime.UtcNow);
                    ModelState.AddModelError("ResolutionId", "The selected resolution does not exist.");
                }
            }

            // Ensure Name is not empty or whitespace
            if (string.IsNullOrWhiteSpace(comment.Name))
            {
                _logger.LogWarning("CreateComment: Name is empty; Adding validation error at {Timestamp}", DateTime.UtcNow);
                ModelState.AddModelError("Name", "The Name field is required.");
            }

            // Validate ErrorCode uniqueness for the given ResolutionId
            if (!string.IsNullOrEmpty(comment.ErrorCode))
            {
                var errorCodeExists = await _context.Comments
                    .AnyAsync(c => c.ResolutionId == comment.ResolutionId && c.ErrorCode == comment.ErrorCode);
                _logger.LogInformation("CreateComment: Checking ErrorCode={ErrorCode} for ResolutionId={ResolutionId}; Exists={ErrorCodeExists} at {Timestamp}",
                    comment.ErrorCode, comment.ResolutionId, errorCodeExists, DateTime.UtcNow);
                if (errorCodeExists)
                {
                    _logger.LogWarning("CreateComment: Duplicate ErrorCode={ErrorCode} found for ResolutionId={ResolutionId}; Adding validation error at {Timestamp}",
                        comment.ErrorCode, comment.ResolutionId, DateTime.UtcNow);
                    ModelState.AddModelError("ErrorCode", $"The Error Code '{comment.ErrorCode}' already exists for this resolution.");
                }
            }

            // Log final ModelState errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("CreateComment: Final ModelState invalid; Errors={Errors} at {Timestamp}", System.Text.Json.JsonSerializer.Serialize(errors), DateTime.UtcNow);
                TempData["ErrorMessage"] = "Please correct the errors in the form: " + string.Join("; ", errors.SelectMany(e => e.Value));
                var resolution = comment.ResolutionId != 0 ? await _context.Resolutions.FindAsync(comment.ResolutionId) : null;
                ViewBag.ResolutionName = resolution?.Name ?? "None";
                //_logger.LogInformation("CreateComment: Returning view with ResolutionId={ResolutionId}, ResolutionName={ResolutionName} at {Timestamp}", comment.ResolutionId, ViewBag.ResolutionName, DateTime.UtcNow);
                return View(comment);
            }

            try
            {
                _logger.LogInformation("CreateComment: Attempting to save comment; Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode} at {Timestamp}",
                    comment.Name, comment.ResolutionId, comment.ErrorCode, DateTime.UtcNow);
                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                _logger.LogInformation("CreateComment: Comment saved successfully; Id={Id}, Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode} at {Timestamp}",
                    comment.Id, comment.Name, comment.ResolutionId, comment.ErrorCode, DateTime.UtcNow);
                TempData["SuccessMessage"] = "Comment created successfully.";
                return RedirectToAction(nameof(EditComments));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "CreateComment: Error saving comment; Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode}, Message={Message}, InnerException={InnerException} at {Timestamp}",
                    comment.Name ?? "null", comment.ResolutionId, comment.ErrorCode, ex.Message, ex.InnerException?.Message, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while creating the comment: " + (ex.InnerException?.Message ?? ex.Message);
                var resolution = comment.ResolutionId != 0 ? await _context.Resolutions.FindAsync(comment.ResolutionId) : null;
                ViewBag.ResolutionName = resolution?.Name ?? "None";
                //_logger.LogInformation("CreateComment: Returning view with ResolutionId={ResolutionId}, ResolutionName={ResolutionName} at {Timestamp}", comment.ResolutionId, ViewBag.ResolutionName, DateTime.UtcNow);
                return View(comment);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditComment(int id)
        {
            _logger.LogInformation("GET /Admin/EditComment called for comment ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditComment for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var comment = await _context.Comments
                .Include(c => c.Resolution)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (comment == null)
            {
                _logger.LogWarning("Comment not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Comment not found.";
                return RedirectToAction(nameof(EditComments));
            }
            ViewBag.Resolutions = await _context.Resolutions.OrderBy(r => r.Name).ToListAsync();
            return View(comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int id, [Bind("Id,Name,ErrorCode,ResolutionId")] Comment comment)
        {
            _logger.LogInformation("POST /Admin/EditComment called for comment ID: {Id}, Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode}, user: {UserName} at {Timestamp}",
                id, comment.Name, comment.ResolutionId, comment.ErrorCode ?? "null", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditComment POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            if (id != comment.Id)
            {
                _logger.LogWarning("Comment ID mismatch: URL ID {UrlId} does not match form ID {FormId} for user: {UserName} at {Timestamp}", id, comment.Id, User.Identity?.Name, DateTime.UtcNow);
                return NotFound();
            }

            // Log raw form data
            _logger.LogInformation("EditComment: Raw form data: {FormData}", string.Join("; ", Request.Form.Select(f => $"{f.Key}={f.Value}")));

            // Remove erroneous Resolution validation
            if (ModelState.ContainsKey("Resolution"))
            {
                _logger.LogInformation("EditComment: Clearing erroneous 'Resolution' ModelState entry at {Timestamp}", DateTime.UtcNow);
                ModelState.Remove("Resolution");
            }

            // Validate ResolutionId
            if (comment.ResolutionId <= 0)
            {
                _logger.LogWarning("EditComment: Invalid ResolutionId={ResolutionId}; Adding validation error at {Timestamp}", comment.ResolutionId, DateTime.UtcNow);
                ModelState.AddModelError("ResolutionId", "The Resolution field is required.");
            }
            else
            {
                var resolutionExists = await _context.Resolutions.AnyAsync(r => r.Id == comment.ResolutionId);
                _logger.LogInformation("EditComment: ResolutionId={ResolutionId} from form; Exists in DB={ResolutionExists} at {Timestamp}", comment.ResolutionId, resolutionExists, DateTime.UtcNow);
                if (!resolutionExists)
                {
                    _logger.LogWarning("EditComment: ResolutionId={ResolutionId} does not exist in DB; Adding validation error at {Timestamp}", comment.ResolutionId, DateTime.UtcNow);
                    ModelState.AddModelError("ResolutionId", "The selected resolution does not exist.");
                }
            }

            // Ensure Name is not empty or whitespace
            if (string.IsNullOrWhiteSpace(comment.Name))
            {
                _logger.LogWarning("EditComment: Name is empty; Adding validation error at {Timestamp}", DateTime.UtcNow);
                ModelState.AddModelError("Name", "The Name field is required.");
            }

            // Validate ErrorCode uniqueness for the given ResolutionId, excluding the current comment
            if (!string.IsNullOrEmpty(comment.ErrorCode))
            {
                var errorCodeExists = await _context.Comments
                    .AnyAsync(c => c.ResolutionId == comment.ResolutionId && c.ErrorCode == comment.ErrorCode && c.Id != comment.Id);
                _logger.LogInformation("EditComment: Checking ErrorCode={ErrorCode} for ResolutionId={ResolutionId}; Exists={ErrorCodeExists} at {Timestamp}",
                    comment.ErrorCode, comment.ResolutionId, errorCodeExists, DateTime.UtcNow);
                if (errorCodeExists)
                {
                    _logger.LogWarning("EditComment: Duplicate ErrorCode={ErrorCode} found for ResolutionId={ResolutionId}; Adding validation error at {Timestamp}",
                        comment.ErrorCode, comment.ResolutionId, DateTime.UtcNow);
                    ModelState.AddModelError("ErrorCode", $"The Error Code '{comment.ErrorCode}' already exists for this resolution.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingComment = await _context.Comments
                        .Include(c => c.Resolution)
                        .FirstOrDefaultAsync(c => c.Id == id);
                    if (existingComment == null)
                    {
                        _logger.LogWarning("Comment not found during update: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                        return NotFound();
                    }

                    existingComment.Name = comment.Name;
                    existingComment.ErrorCode = comment.ErrorCode;
                    existingComment.ResolutionId = comment.ResolutionId;

                    _context.Update(existingComment);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Comment updated successfully: ID {Id}, Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode} for user: {UserName} at {Timestamp}",
                        id, comment.Name, comment.ResolutionId, comment.ErrorCode, User.Identity?.Name, DateTime.UtcNow);
                    TempData["SuccessMessage"] = "Comment updated successfully.";
                    return RedirectToAction(nameof(EditComments));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error updating comment ID: {Id}, Name={Name}, ResolutionId={ResolutionId}, ErrorCode={ErrorCode} for user: {UserName} at {Timestamp}",
                        id, comment.Name, comment.ResolutionId, comment.ErrorCode, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "An error occurred while updating the comment: " + (ex.InnerException?.Message ?? ex.Message);
                }
            }
            else
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList());
                _logger.LogWarning("EditComment: ModelState invalid for comment ID: {Id}, user: {UserName}, errors: {Errors} at {Timestamp}",
                    id, User.Identity?.Name, System.Text.Json.JsonSerializer.Serialize(errors), DateTime.UtcNow);
                TempData["ErrorMessage"] = "Please correct the errors in the form: " + string.Join("; ", errors.SelectMany(e => e.Value));
            }

            comment.Resolution = await _context.Resolutions.FindAsync(comment.ResolutionId);
            ViewBag.Resolutions = await _context.Resolutions.OrderBy(r => r.Name).ToListAsync();
            return View(comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            _logger.LogInformation("POST /Admin/DeleteComment called for comment ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in DeleteComment for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                _logger.LogWarning("Comment not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Comment not found.";
                return RedirectToAction(nameof(EditComments));
            }

            try
            {
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Comment deleted successfully: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["SuccessMessage"] = "Comment deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting comment ID: {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while deleting the comment.";
            }
            return RedirectToAction(nameof(EditComments));
        }

        [HttpGet]
        public async Task<IActionResult> EditResolutions()
        {
            _logger.LogInformation("GET /Admin/EditResolutions called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditResolutions for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var resolutions = await _context.Resolutions.ToListAsync();
            _logger.LogInformation("Retrieved {Count} resolutions for EditResolutions at {Timestamp}", resolutions.Count, DateTime.UtcNow);
            return View(resolutions);
        }

        [HttpGet]
        public async Task<IActionResult> EditResolution(int? id = null)
        {
            _logger.LogInformation("GET /Admin/EditResolution called for resolution ID: {Id}, user: {UserName} at {Timestamp}", id ?? 0, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditResolution for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            Resolution model;
            if (id.HasValue)
            {
                model = await _context.Resolutions.FirstOrDefaultAsync(r => r.Id == id.Value);
                if (model == null)
                {
                    _logger.LogWarning("Resolution not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "Resolution not found.";
                    return RedirectToAction(nameof(EditResolutions));
                }
            }
            else
            {
                model = new Resolution();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditResolution(int id, [Bind("Id,Name")] Resolution resolution)
        {
            _logger.LogInformation("POST /Admin/EditResolution called for resolution ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditResolution POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            if (id != resolution.Id && id != 0)
            {
                _logger.LogWarning("Resolution ID mismatch: URL ID {UrlId} does not match form ID {FormId} for user: {UserName} at {Timestamp}", id, resolution.Id, User.Identity?.Name, DateTime.UtcNow);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (resolution.Id == 0)
                    {
                        _context.Resolutions.Add(resolution);
                        _logger.LogInformation("Creating new resolution: Name={Name} for user: {UserName} at {Timestamp}", resolution.Name, User.Identity?.Name, DateTime.UtcNow);
                    }
                    else
                    {
                        var existingResolution = await _context.Resolutions.FirstOrDefaultAsync(r => r.Id == resolution.Id);
                        if (existingResolution == null)
                        {
                            _logger.LogWarning("Resolution not found during update: ID {Id} for user: {UserName} at {Timestamp}", resolution.Id, User.Identity?.Name, DateTime.UtcNow);
                            return NotFound();
                        }
                        existingResolution.Name = resolution.Name;
                        _context.Update(existingResolution);
                        _logger.LogInformation("Resolution updated: ID {Id}, Name={Name} for user: {UserName} at {Timestamp}", resolution.Id, resolution.Name, User.Identity?.Name, DateTime.UtcNow);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = resolution.Id == 0 ? "Resolution created successfully." : "Resolution updated successfully.";
                    return RedirectToAction(nameof(EditResolutions));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error saving/updating resolution ID: {Id} for user: {UserName} at {Timestamp}", resolution.Id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "An error occurred while saving the resolution.";
                }
            }
            else
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("ModelState invalid for resolution ID: {Id}, user: {UserName}, errors: {Errors} at {Timestamp}", resolution.Id, User.Identity?.Name, errors, DateTime.UtcNow);
            }

            return View(resolution);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResolution(int id)
        {
            _logger.LogInformation("POST /Admin/DeleteResolution called for resolution ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in DeleteResolution for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var resolution = await _context.Resolutions.FindAsync(id);
            if (resolution == null)
            {
                _logger.LogWarning("Resolution not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Resolution not found.";
                return RedirectToAction(nameof(EditResolutions));
            }

            try
            {
                var comments = await _context.Comments.Where(c => c.ResolutionId == id).ToListAsync();
                foreach (var comment in comments)
                {
                    comment.ResolutionId = 0;
                    _context.Update(comment);
                }
                _logger.LogInformation("Orphaned {Count} comments for resolution ID: {Id} at {Timestamp}", comments.Count, id, DateTime.UtcNow);

                _context.Resolutions.Remove(resolution);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Resolution deleted successfully: ID {Id}, Name={Name} for user: {UserName} at {Timestamp}", id, resolution.Name, User.Identity?.Name, DateTime.UtcNow);
                TempData["SuccessMessage"] = "Resolution deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting resolution ID: {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while deleting the resolution. It may be referenced by tickets.";
            }
            return RedirectToAction(nameof(EditResolutions));
        }

        [HttpGet]
        public async Task<IActionResult> EditSitaContracts()
        {
            _logger.LogInformation("GET /Admin/EditSitaContracts called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditSitaContracts for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var contracts = await _context.SitaContracts.ToListAsync();
            _logger.LogInformation("Retrieved {Count} contracts for EditSitaContracts at {Timestamp}", contracts.Count, DateTime.UtcNow);
            return View(contracts);
        }

        [HttpGet]
        public async Task<IActionResult> EditSitaContract(int? id = null)
        {
            _logger.LogInformation("GET /Admin/EditSitaContract called for contract ID: {Id}, user: {UserName} at {Timestamp}", id ?? 0, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditSitaContract for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            SitaContract model;
            if (id.HasValue)
            {
                model = await _context.SitaContracts.FirstOrDefaultAsync(c => c.Id == id.Value);
                if (model == null)
                {
                    _logger.LogWarning("Contract not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "Contract not found.";
                    return RedirectToAction(nameof(EditSitaContracts));
                }
            }
            else
            {
                model = new SitaContract();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSitaContract(int id, [Bind("Id,CompanyName")] SitaContract contract)
        {
            _logger.LogInformation("POST /Admin/EditSitaContract called for contract ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditSitaContract POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            if (id != contract.Id && id != 0)
            {
                _logger.LogWarning("Contract ID mismatch: URL ID {UrlId} does not match form ID {FormId} for user: {UserName} at {Timestamp}", id, contract.Id, User.Identity?.Name, DateTime.UtcNow);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (contract.Id == 0)
                    {
                        _context.SitaContracts.Add(contract);
                        _logger.LogInformation("Creating new contract: CompanyName={CompanyName} for user: {UserName} at {Timestamp}", contract.CompanyName, User.Identity?.Name, DateTime.UtcNow);
                    }
                    else
                    {
                        var existingContract = await _context.SitaContracts.FirstOrDefaultAsync(c => c.Id == contract.Id);
                        if (existingContract == null)
                        {
                            _logger.LogWarning("Contract not found during update: ID {Id} for user: {UserName} at {Timestamp}", contract.Id, User.Identity?.Name, DateTime.UtcNow);
                            return NotFound();
                        }
                        existingContract.CompanyName = contract.CompanyName;
                        _context.Update(existingContract);
                        _logger.LogInformation("Contract updated: ID {Id}, CompanyName={CompanyName} for user: {UserName} at {Timestamp}", contract.Id, contract.CompanyName, User.Identity?.Name, DateTime.UtcNow);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = contract.Id == 0 ? "Contract created successfully." : "Contract updated successfully.";
                    return RedirectToAction(nameof(EditSitaContracts));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error saving/updating contract ID: {Id} for user: {UserName} at {Timestamp}", contract.Id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "An error occurred while saving the contract.";
                }
            }
            else
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("ModelState invalid for contract ID: {Id}, user: {UserName}, errors: {Errors} at {Timestamp}", contract.Id, User.Identity?.Name, errors, DateTime.UtcNow);
            }

            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSitaContract(int id)
        {
            _logger.LogInformation("POST /Admin/DeleteSitaContract called for contract ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in DeleteSitaContract for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var contract = await _context.SitaContracts.FindAsync(id);
            if (contract == null)
            {
                _logger.LogWarning("Contract not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Contract not found.";
                return RedirectToAction(nameof(EditSitaContracts));
            }

            try
            {
                var noteCount = await _context.Notes.CountAsync(n => n.CompanyId == id);
                var howToCount = await _context.HowTos.CountAsync(h => h.CompanyId == id);
                if (noteCount > 0 || howToCount > 0)
                {
                    _logger.LogWarning("Cannot delete contract ID {Id} as it is referenced by {NoteCount} notes and {HowToCount} how-tos at {Timestamp}", id, noteCount, howToCount, DateTime.UtcNow);
                    TempData["ErrorMessage"] = $"Cannot delete contract as it is referenced by {noteCount} note(s) and {howToCount} how-to(s).";
                    return RedirectToAction(nameof(EditSitaContracts));
                }

                _context.SitaContracts.Remove(contract);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Contract deleted successfully: ID {Id}, CompanyName={CompanyName} for user: {UserName} at {Timestamp}", id, contract.CompanyName, User.Identity?.Name, DateTime.UtcNow);
                TempData["SuccessMessage"] = "Contract deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting contract ID: {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while deleting the contract. It may be referenced by other entities.";
            }
            return RedirectToAction(nameof(EditSitaContracts));
        }

        [HttpGet]
        public async Task<IActionResult> EditTeamMembers()
        {
            _logger.LogInformation("GET /Admin/EditTeamMembers called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditTeamMembers for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var teamMembers = await _context.SitaTeamMembers.ToListAsync();
            _logger.LogInformation("Retrieved {Count} team members for EditTeamMembers at {Timestamp}", teamMembers.Count, DateTime.UtcNow);
            return View(teamMembers);
        }

        [HttpGet]
        public async Task<IActionResult> EditTeamMember(int? id = null)
        {
            _logger.LogInformation("GET /Admin/EditTeamMember called for team member ID: {Id}, user: {UserName} at {Timestamp}", id ?? 0, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditTeamMember for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            SitaTeamMember model;
            if (id.HasValue)
            {
                model = await _context.SitaTeamMembers.FirstOrDefaultAsync(t => t.Id == id.Value);
                if (model == null)
                {
                    _logger.LogWarning("Team member not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "Team member not found.";
                    return RedirectToAction(nameof(EditTeamMembers));
                }
            }
            else
            {
                model = new SitaTeamMember();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeamMember(int id, [Bind("Id,Name")] SitaTeamMember teamMember)
        {
            _logger.LogInformation("POST /Admin/EditTeamMember called for team member ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditTeamMember POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            if (id != teamMember.Id && id != 0)
            {
                _logger.LogWarning("Team member ID mismatch: URL ID {UrlId} does not match form ID {FormId} for user: {UserName} at {Timestamp}", id, teamMember.Id, User.Identity?.Name, DateTime.UtcNow);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (teamMember.Id == 0)
                    {
                        _context.SitaTeamMembers.Add(teamMember);
                        _logger.LogInformation("Creating new team member: Name={Name} for user: {UserName} at {Timestamp}", teamMember.Name, User.Identity?.Name, DateTime.UtcNow);
                    }
                    else
                    {
                        var existingTeamMember = await _context.SitaTeamMembers.FirstOrDefaultAsync(t => t.Id == teamMember.Id);
                        if (existingTeamMember == null)
                        {
                            _logger.LogWarning("Team member not found during update: ID {Id} for user: {UserName} at {Timestamp}", teamMember.Id, User.Identity?.Name, DateTime.UtcNow);
                            return NotFound();
                        }
                        existingTeamMember.Name = teamMember.Name;
                        _context.Update(existingTeamMember);
                        _logger.LogInformation("Team member updated: ID {Id}, Name={Name} for user: {UserName} at {Timestamp}", teamMember.Id, teamMember.Name, User.Identity?.Name, DateTime.UtcNow);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = teamMember.Id == 0 ? "Team member created successfully." : "Team member updated successfully.";
                    return RedirectToAction(nameof(EditTeamMembers));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error saving/updating team member ID: {Id} for user: {UserName} at {Timestamp}", teamMember.Id, User.Identity?.Name, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "An error occurred while saving the team member.";
                }
            }
            else
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("ModelState invalid for team member ID: {Id}, user: {UserName}, errors: {Errors} at {Timestamp}", teamMember.Id, User.Identity?.Name, errors, DateTime.UtcNow);
            }

            return View(teamMember);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeamMember(int id)
        {
            _logger.LogInformation("POST /Admin/DeleteTeamMember called for team member ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in DeleteTeamMember for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var teamMember = await _context.SitaTeamMembers.FindAsync(id);
            if (teamMember == null)
            {
                _logger.LogWarning("Team member not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Team member not found.";
                return RedirectToAction(nameof(EditTeamMembers));
            }

            try
            {
                var ticketCount = await _context.Tickets.CountAsync(t => t.Employee == teamMember.Name);
                if (ticketCount > 0)
                {
                    _logger.LogWarning("Cannot delete team member ID {Id} as it is referenced by {Count} tickets at {Timestamp}", id, ticketCount, DateTime.UtcNow);
                    TempData["ErrorMessage"] = $"Cannot delete team member as it is referenced by {ticketCount} ticket(s).";
                    return RedirectToAction(nameof(EditTeamMembers));
                }

                _context.SitaTeamMembers.Remove(teamMember);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Team member deleted successfully: ID {Id}, Name={Name} for user: {UserName} at {Timestamp}", id, teamMember.Name, User.Identity?.Name, DateTime.UtcNow);
                TempData["SuccessMessage"] = "Team member deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting team member ID: {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while deleting the team member. It may be referenced by other entities.";
            }
            return RedirectToAction(nameof(EditTeamMembers));
        }

        [HttpGet]
        public async Task<IActionResult> EditUsers()
        {
            _logger.LogInformation("GET /Admin/EditUsers called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in EditUsers for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            var users = await _context.Users.ToListAsync();
            _logger.LogInformation("Retrieved {Count} users for EditUsers at {Timestamp}", users.Count, DateTime.UtcNow);
            return View(users);
        }

        [HttpGet]
        public IActionResult RegisterUser()
        {
            _logger.LogInformation("GET /Admin/RegisterUser called for user: {UserName} at {Timestamp}", User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in RegisterUser for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));
            return View(new RegisterUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterUser(RegisterUserViewModel model)
        {
            _logger.LogInformation("POST /Admin/RegisterUser called for email: {Email}, IsAdmin: {IsAdmin}, user: {UserName} at {Timestamp}",
                model.Email, model.IsAdmin, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in RegisterUser POST for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    Surname = model.Surname,
                    IsAdmin = model.IsAdmin
                };

                try
                {
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                    {
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole("Admin"));
                        if (!roleResult.Succeeded)
                        {
                            _logger.LogError("Failed to create Admin role: {Errors}", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                            TempData["ErrorMessage"] = "Failed to create Admin role.";
                            return View(model);
                        }
                    }
                    if (!await _roleManager.RoleExistsAsync("User"))
                    {
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole("User"));
                        if (!roleResult.Succeeded)
                        {
                            _logger.LogError("Failed to create User role: {Errors}", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                            TempData["ErrorMessage"] = "Failed to create User role.";
                            return View(model);
                        }
                    }

                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        var role = model.IsAdmin ? "Admin" : "User";
                        var roleAssignResult = await _userManager.AddToRoleAsync(user, role);
                        if (!roleAssignResult.Succeeded)
                        {
                            _logger.LogError("Failed to assign {Role} to user {Email}: {Errors}", role, model.Email, string.Join("; ", roleAssignResult.Errors.Select(e => e.Description)));
                            await _userManager.DeleteAsync(user);
                            TempData["ErrorMessage"] = $"Failed to assign {role} role.";
                            return View(model);
                        }

                        _logger.LogInformation("User registered successfully: Email={Email}, IsAdmin={IsAdmin} at {Timestamp}", model.Email, model.IsAdmin, DateTime.UtcNow);
                        TempData["SuccessMessage"] = "User registered successfully.";
                        return RedirectToAction(nameof(EditUsers));
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                        _logger.LogError("User creation error for {Email}: {Description} at {Timestamp}", model.Email, error.Description, DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error registering user {Email} at {Timestamp}", model.Email, DateTime.UtcNow);
                    TempData["ErrorMessage"] = "An error occurred while registering the user.";
                }
            }
            else
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("ModelState invalid for user registration {Email}: {Errors} at {Timestamp}", model.Email, errors, DateTime.UtcNow);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            _logger.LogInformation("POST /Admin/DeleteUser called for user ID: {Id}, user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogDebug("Claims in DeleteUser for {UserName}: {Claims}", User.Identity?.Name, string.Join("; ", claims));

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found: ID {Id} for user: {UserName} at {Timestamp}", id, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(EditUsers));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.Id == id)
            {
                _logger.LogWarning("Attempt to delete current user {Email} by {UserName} at {Timestamp}", user.Email, User.Identity?.Name, DateTime.UtcNow);
                TempData["ErrorMessage"] = "Cannot delete your own account.";
                return RedirectToAction(nameof(EditUsers));
            }

            try
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User deleted successfully: ID {Id}, Email={Email} for user: {UserName} at {Timestamp}", id, user.Email, User.Identity?.Name, DateTime.UtcNow);
                    TempData["SuccessMessage"] = "User deleted successfully.";
                }
                else
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Error deleting user ID {Id}, Email={Email}: {Errors} at {Timestamp}", id, user.Email, errors, DateTime.UtcNow);
                    TempData["ErrorMessage"] = $"Failed to delete user: {errors}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user ID: {Id}, Email={Email} at {Timestamp}", id, user.Email, DateTime.UtcNow);
                TempData["ErrorMessage"] = "An error occurred while deleting the user.";
            }

            return RedirectToAction(nameof(EditUsers));
        }
    }

    public class LoginHistoryViewModel
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public DateTime LoginTime { get; set; }
        public string IpAddress { get; set; }
    }
}