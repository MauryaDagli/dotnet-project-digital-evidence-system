using DECMS.Data;
using DECMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;

namespace DECMS.Controllers;

[Authorize]
public class CasesController : Controller
{
    private readonly MongoDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CasesController(MongoDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string searchString)
    {
        ViewData["CurrentFilter"] = searchString;
        var cases = await _context.GetCasesListAsync(searchString);
        return View(cases);
    }

    public async Task<IActionResult> Details(string? id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var @case = await _context.GetCaseWithDetailsAsync(id);
        if (@case == null) return NotFound();

        return View(@case);
    }

    [Authorize(Roles = "Admin,Police Officer")]
    public async Task<IActionResult> Create()
    {
        var investigators = await _userManager.GetUsersInRoleAsync("Investigator");
        var officers = await _userManager.GetUsersInRoleAsync("Police Officer");
        var assignable = investigators.Concat(officers).ToList();
        ViewData["AssignedOfficerId"] = new SelectList(assignable, "Id", "FullName");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Police Officer")]
    public async Task<IActionResult> Create([Bind("CaseNumber,Title,Description,Status,Priority,IncidentDate,Location,AssignedOfficerId")] Case @case)
    {
        if (string.IsNullOrWhiteSpace(@case.CaseNumber))
        {
            var count = await _context.Cases.CountDocumentsAsync(Builders<Case>.Filter.Empty);
            @case.CaseNumber = $"CASE-{DateTime.UtcNow.Year}-{(count + 1):D4}";
            ModelState.Remove("CaseNumber");
        }

        ModelState.Remove("CreatedBy");
        ModelState.Remove("CreatedById");
        ModelState.Remove("Evidences");

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                @case.CreatedById = user.Id;
            }
            @case.CreatedAt = DateTime.UtcNow;

            await _context.Cases.InsertOneAsync(@case);

            // Audit Log
            await _context.AuditLogs.InsertOneAsync(new AuditLog
            {
                UserId = user?.Id ?? "System",
                Action = "Create Case",
                EntityType = "Case",
                EntityId = @case.CaseNumber,
                Description = $"Case {@case.CaseNumber} created in MongoDB."
            });

            TempData["SuccessMessage"] = "Case successfully created.";
            return RedirectToAction(nameof(Index));
        }

        var assignable = (await _userManager.GetUsersInRoleAsync("Investigator")).Concat(await _userManager.GetUsersInRoleAsync("Police Officer")).ToList();
        ViewData["AssignedOfficerId"] = new SelectList(assignable, "Id", "FullName", @case.AssignedOfficerId);
        return View(@case);
    }
}
