using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SitaCptTicketApp.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Areas.Identity.Pages
{
    [Authorize(Roles = "Admin")]
    public class ViewLoginHistoryModel : PageModel
    {
        private readonly SitaCptTicketAppContext _context;

        public ViewLoginHistoryModel(SitaCptTicketAppContext context)
        {
            _context = context;
        }

        public IList<LoginHistory> LoginHistory { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;

        public async Task OnGetAsync(int pageNumber = 1)
        {
            CurrentPage = pageNumber < 1 ? 1 : pageNumber;
            var skip = (CurrentPage - 1) * PageSize;
            var totalRecords = await _context.LoginHistory.CountAsync();
            TotalPages = (int)Math.Ceiling((double)totalRecords / PageSize);

            LoginHistory = await _context.LoginHistory
                .Include(lh => lh.User) // Include ApplicationUser for user details
                .OrderByDescending(lh => lh.LoginTime)
                .Skip(skip)
                .Take(PageSize)
                .ToListAsync();
        }
    }

}