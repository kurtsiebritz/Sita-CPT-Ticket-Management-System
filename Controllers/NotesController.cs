using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Controllers
{
    [Authorize]
    public class NotesController : Controller
    {
        private readonly SitaCptTicketAppContext _context;
        private readonly string _documentStoragePath;

        public NotesController(SitaCptTicketAppContext context, IWebHostEnvironment env)
        {
            _context = context;
            _documentStoragePath = Path.Combine(env.WebRootPath, "note-documents");
            if (!Directory.Exists(_documentStoragePath))
            {
                Directory.CreateDirectory(_documentStoragePath);
            }
        }

        // GET: /Notes/
        public async Task<IActionResult> Index()
        {
            var notes = await _context.Notes
                .Include(n => n.Company)
                .Include(n => n.CreatedBy)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();
            return View(notes);
        }

        // GET: /Notes/Create
        public async Task<IActionResult> Create()
        {
            var model = new NoteViewModel
            {
                Companies = new SelectList(await _context.SitaContracts.ToListAsync(), "Id", "CompanyName"),
                CreatedByTeamMembers = new SelectList(await _context.SitaTeamMembers.OrderBy(t => t.Name).ToListAsync(), "Id", "Name")
            };
            return View(model);
        }

        // POST: /Notes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NoteViewModel model, IFormFile document)
        {
            // Log form data for debugging
            System.Diagnostics.Debug.WriteLine($"Form data: Title='{model.Title}', CompanyId={model.CompanyId}, Content='{model.Content}', CreatedById={model.CreatedById}, Document={(document != null ? document.FileName : "null")}");

            // Remove non-submitted fields from ModelState validation
            ModelState.Remove("Companies");
            ModelState.Remove("CreatedByTeamMembers");
            ModelState.Remove("document");

            if (!ModelState.IsValid)
            {
                // Log detailed ModelState errors
                var errors = ModelState.Select(kvp => $"{kvp.Key}: {string.Join("; ", kvp.Value.Errors.Select(e => e.ErrorMessage))}");
                System.Diagnostics.Debug.WriteLine("ModelState errors: " + string.Join("; ", errors));
                TempData["Error"] = "Failed to create note: " + string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            }
            else
            {
                string documentPath = null;
                if (document != null && IsValidDocument(document))
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(document.FileName);
                    var filePath = Path.Combine(_documentStoragePath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await document.CopyToAsync(stream);
                    }
                    documentPath = $"/note-documents/{fileName}";
                }

                var note = new Note
                {
                    Title = model.Title,
                    CompanyId = model.CompanyId,
                    Content = model.Content,
                    DocumentPath = documentPath,
                    CreatedById = model.CreatedById,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Notes.Add(note);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Note created successfully!";
                return RedirectToAction(nameof(Index));
            }

            // Repopulate dropdowns for the view
            model.Companies = new SelectList(await _context.SitaContracts.ToListAsync(), "Id", "CompanyName", model.CompanyId);
            model.CreatedByTeamMembers = new SelectList(await _context.SitaTeamMembers.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", model.CreatedById);
            return View(model);
        }

        // GET: /Notes/View/5
        public async Task<IActionResult> View(int id)
        {
            var note = await _context.Notes
                .Include(n => n.Company)
                .Include(n => n.CreatedBy)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (note == null)
            {
                return NotFound();
            }

            return View(note);
        }

        // GET: /Notes/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var note = await _context.Notes
                .Include(n => n.Company)
                .Include(n => n.CreatedBy)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (note == null)
            {
                return NotFound();
            }

            return View(note);
        }

        // POST: /Notes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var note = await _context.Notes.FindAsync(id);
            if (note == null)
            {
                return NotFound();
            }

            // Delete associated document if it exists
            if (!string.IsNullOrEmpty(note.DocumentPath))
            {
                var filePath = Path.Combine(_documentStoragePath, Path.GetFileName(note.DocumentPath));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted document: {filePath}");
                }
            }

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"Deleted note with Id: {id}");
            TempData["Success"] = "Note deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool IsValidDocument(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension) && file.Length < 10 * 1024 * 1024; // 10MB limit
        }
    }

    public class NoteViewModel
    {
            [Required(ErrorMessage = "Title is required.")]
            [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters.")]
            public string Title { get; set; }

            [Required(ErrorMessage = "Company is required.")]
            public int CompanyId { get; set; }

            [Required(ErrorMessage = "Content is required.")]
            public string Content { get; set; }

            [Required(ErrorMessage = "Created by team member is required.")]
            public int CreatedById { get; set; }

            public SelectList Companies { get; set; }
            public SelectList CreatedByTeamMembers { get; set; }
    }
}