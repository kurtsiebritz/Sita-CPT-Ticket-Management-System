using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Controllers
{
    [Authorize]
    public class HowToController : Controller
    {
        private readonly SitaCptTicketAppContext _context;
        private readonly string _imageStoragePath;

        public HowToController(SitaCptTicketAppContext context, IWebHostEnvironment env)
        {
            _context = context;
            _imageStoragePath = Path.Combine(env.WebRootPath, "how-to-images");
            if (!Directory.Exists(_imageStoragePath))
            {
                Directory.CreateDirectory(_imageStoragePath);
            }
        }

        public async Task<IActionResult> Index(int? contractId, string search)
        {
            var contracts = await _context.SitaContracts.ToListAsync();
            ViewBag.Contracts = new SelectList(contracts, "Id", "CompanyName", contractId);
            ViewBag.Search = search;

            var howTos = await _context.HowTos
                .Include(h => h.Steps)
                .Include(h => h.Company)
                .Include(h => h.CreatedBy)
                .Where(h => !contractId.HasValue || h.CompanyId == contractId.Value)
                .Where(h => string.IsNullOrEmpty(search) || EF.Functions.Like(h.Title, $"%{search}%"))
                .OrderByDescending(h => h.CreatedDate)
                .ToListAsync();

            Console.WriteLine($"Searching How-To Tutorials by Contract ID: {contractId?.ToString() ?? "none"}, Search Term: {search ?? "none"}, Results: {howTos.Count}");
            return View(howTos);
        }

        [HttpGet]
        public async Task<IActionResult> SearchSuggestions(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Json(new List<string>());
            }

            var suggestions = await _context.HowTos
                .Where(h => EF.Functions.Like(h.Title, $"%{term}%"))
                .Select(h => h.Title)
                .Distinct()
                .Take(5)
                .ToListAsync();

            Console.WriteLine($"Suggestions for '{term}': {string.Join(", ", suggestions)}");
            return Json(suggestions);
        }

        public async Task<IActionResult> Create()
        {
            var contracts = await _context.SitaContracts.ToListAsync();
            var teamMembers = await _context.SitaTeamMembers.OrderBy(t => t.Name).ToListAsync();
            Console.WriteLine($"SitaContracts count: {contracts.Count}, SitaTeamMembers count: {teamMembers.Count}");
            var model = new HowToViewModel
            {
                Companies = new SelectList(contracts, "Id", "CompanyName"),
                TeamMembers = new SelectList(teamMembers, "Id", "Name"),
                Steps = new List<HowToStepViewModel> { new HowToStepViewModel { OrderIndex = 1 } }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HowToViewModel model)
        {
            Console.WriteLine($"Create How-To: CompanyId={model.CompanyId}, CreatedById={model.CreatedById}, Steps count={model.Steps?.Count ?? 0}, Images={model.Steps?.Count(s => s.Image != null) ?? 0}");
            ModelState.Remove("Companies");
            ModelState.Remove("TeamMembers");
            foreach (var key in ModelState.Keys.Where(k => k.Contains(".Image")))
            {
                ModelState.Remove(key); // Ensure images are not required
            }
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key].Errors;
                if (errors.Any())
                {
                    Console.WriteLine($"ModelState Error for {key}: {string.Join("; ", errors.Select(e => e.ErrorMessage))}");
                }
            }

            if (!ModelState.IsValid || !model.Steps.All(s => !string.IsNullOrEmpty(s.Instructions)))
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Error"] = errors.Any() ? string.Join("; ", errors) : "Please fill in all required fields, including a valid company, team member, and step instructions.";
                var contracts = await _context.SitaContracts.ToListAsync();
                var teamMembers = await _context.SitaTeamMembers.OrderBy(t => t.Name).ToListAsync();
                model.Companies = new SelectList(contracts, "Id", "CompanyName", model.CompanyId);
                model.TeamMembers = new SelectList(teamMembers, "Id", "Name", model.CreatedById);
                return View(model);
            }

            var howTo = new HowTo
            {
                Title = model.Title,
                CompanyId = model.CompanyId,
                CreatedById = model.CreatedById,
                CreatedDate = DateTime.UtcNow,
                Steps = new List<HowToStep>()
            };

            for (int i = 0; i < model.Steps.Count; i++)
            {
                var step = model.Steps[i];
                string imagePath = "";
                if (step.Image != null && IsValidImage(step.Image))
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(step.Image.FileName);
                    var filePath = Path.Combine(_imageStoragePath, fileName);
                    Console.WriteLine($"Saving image for step {i + 1} to: {filePath}");
                    try
                    {
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await step.Image.CopyToAsync(stream);
                        }
                        imagePath = $"/how-to-images/{fileName}";
                        Console.WriteLine($"ImagePath set for step {i + 1}: {imagePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving image for step {i + 1}: {ex.Message}");
                        TempData["Error"] = $"Failed to save image for step {i + 1}. Tutorial created without image.";
                        imagePath = "";
                    }
                }
                else if (step.Image == null)
                {
                    Console.WriteLine($"No image provided for step {i + 1}; proceeding without image.");
                }

                howTo.Steps.Add(new HowToStep
                {
                    OrderIndex = step.OrderIndex,
                    Instructions = step.Instructions,
                    ImagePath = imagePath
                });
            }

            _context.HowTos.Add(howTo);
            await _context.SaveChangesAsync();
            TempData["Success"] = "How-To Tutorial created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> View(int id)
        {
            var howTo = await _context.HowTos
                .Include(h => h.Steps)
                .Include(h => h.Company)
                .Include(h => h.CreatedBy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (howTo == null)
            {
                return NotFound();
            }

            Console.WriteLine($"HowTo Id: {id}, Steps count: {howTo.Steps?.Count ?? 0}, ImagePaths: {string.Join(", ", howTo.Steps?.Select(s => s.ImagePath) ?? new List<string>())}");
            return View(howTo);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var howTo = await _context.HowTos
                .Include(h => h.Steps)
                .Include(h => h.Company)
                .Include(h => h.CreatedBy)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (howTo == null)
            {
                return NotFound();
            }

            return View(howTo);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var howTo = await _context.HowTos
                .Include(h => h.Steps)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (howTo == null)
            {
                return NotFound();
            }

            if (howTo.Steps != null)
            {
                foreach (var step in howTo.Steps)
                {
                    if (!string.IsNullOrEmpty(step.ImagePath))
                    {
                        var filePath = Path.Combine(_imageStoragePath, Path.GetFileName(step.ImagePath));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                            Console.WriteLine($"Deleted image: {filePath}");
                        }
                    }
                }
            }

            _context.HowTos.Remove(howTo);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Deleted How-To with Id: {id}");
            TempData["Success"] = "How-To Tutorial deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                Console.WriteLine("IsValidImage: No file provided or file is empty; treated as optional.");
                return false;
            }
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            bool isValid = allowedExtensions.Contains(extension) && file.Length < 5 * 1024 * 1024;
            Console.WriteLine($"IsValidImage: File={file.FileName}, Extension={extension}, Length={file.Length}, Valid={isValid}");
            return isValid;
        }
    }


    public class HowToViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Company is required.")]
        public int CompanyId { get; set; }

        [BindNever]
        public SelectList Companies { get; set; }

        [Required(ErrorMessage = "Team member is required.")]
        public int CreatedById { get; set; }

        [BindNever]
        public SelectList TeamMembers { get; set; }

        public List<HowToStepViewModel> Steps { get; set; }
    }

    public class HowToStepViewModel
    {
        public int OrderIndex { get; set; }

        [Required(ErrorMessage = "Instructions are required.")]
        public string Instructions { get; set; }

        public IFormFile Image { get; set; }
    }
}