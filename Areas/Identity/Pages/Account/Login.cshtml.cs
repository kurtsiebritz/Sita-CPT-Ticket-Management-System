using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SitaCptTicketAppContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            SitaCptTicketAppContext context,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _logger.LogInformation("LoginModel: Initialized at {Time}", DateTime.Now);
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            _logger.LogInformation("Login/OnGet: Entered at {Time}; ReturnUrl={ReturnUrl}", DateTime.Now, returnUrl ?? "none");
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            _logger.LogInformation("Login/OnPost: Entered at {Time}; Email={Email}, ReturnUrl={ReturnUrl}", 
                DateTime.Now, Input?.Email ?? "null", returnUrl ?? "none");
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                _logger.LogInformation("Login/OnPost: Sign-in attempt for {Email}; Succeeded={Succeeded}", Input.Email, result.Succeeded);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(Input.Email);
                    if (user != null)
                    {
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        var loginHistory = new LoginHistory
                        {
                            UserId = user.Id,
                            Email = user.Email,
                            LoginTime = DateTime.UtcNow,
                            IpAddress = ipAddress
                        };
                        _context.LoginHistory.Add(loginHistory);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Login/OnPost: Recorded login for {Email}, UserId={UserId}, IP={IpAddress}", 
                            user.Email, user.Id, ipAddress ?? "null");
                    }
                    else
                    {
                        _logger.LogWarning("Login/OnPost: User not found for email {Email}", Input.Email);
                    }
                    return LocalRedirect(returnUrl);
                }
                _logger.LogWarning("Login/OnPost: Invalid login attempt for {Email}; IsLockedOut={IsLockedOut}, RequiresTwoFactor={RequiresTwoFactor}", 
                    Input.Email, result.IsLockedOut, result.RequiresTwoFactor);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            else
            {
                _logger.LogWarning("Login/OnPost: Invalid model state for {Email}", Input?.Email ?? "null");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Login/OnPost: ModelState error: {Error}", error.ErrorMessage);
                }
            }
            return Page();
        }
    }
}