
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Controllers
{
    [Route("Account")]
    public class AccountController : Controller
    {
        [HttpPost]
        [Route("SignOut")]
        [Authorize]
        public async Task<IActionResult> SignOut()
        {
            // Clear authentication cookie explicitly
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(-1) // Force immediate expiration
            });

            // Clear all cookies to ensure no residual session data
            foreach (var cookie in HttpContext.Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            return Ok();
        }
    }
}