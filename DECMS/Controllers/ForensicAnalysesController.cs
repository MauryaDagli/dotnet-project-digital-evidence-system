using DECMS.Data;
using DECMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DECMS.Controllers;

[Authorize]
public class ForensicAnalysesController : Controller
{
    private readonly MongoDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ForensicAnalysesController(MongoDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize(Roles = "Forensic Analyst,Admin,Investigator,Police Officer")]
    public async Task<IActionResult> Details(string id)
    {
        var analysis = await _context.ForensicAnalyses.Find(f => f.Id == id).FirstOrDefaultAsync();
        if (analysis == null) return NotFound();

        analysis.Evidence = await _context.Evidences.Find(e => e.Id == analysis.EvidenceId).FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(analysis.AnalystId))
            analysis.Analyst = await _context.Users.Find(u => u.Id == analysis.AnalystId).FirstOrDefaultAsync();

        return View(analysis);
    }

    [Authorize(Roles = "Forensic Analyst")]
    public async Task<IActionResult> Create(string evidenceId)
    {
        var evidence = await _context.Evidences.Find(e => e.Id == evidenceId).FirstOrDefaultAsync();
        if (evidence == null) return NotFound();

        ViewBag.EvidenceNumber = evidence.EvidenceNumber;
        ViewBag.EvidenceId = evidenceId;

        return View(new ForensicAnalysis { EvidenceId = evidenceId, AnalysisType = "Standard Forensic Review" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Forensic Analyst")]
    public async Task<IActionResult> Create([Bind("EvidenceId,AnalysisType,Methodology,Findings,ToolsUsed,Notes")] ForensicAnalysis analysis)
    {
        ModelState.Remove("Evidence");
        ModelState.Remove("Analyst");
        ModelState.Remove("AnalystId");

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            analysis.AnalystId = user.Id;
            analysis.StartedAt = DateTime.UtcNow;
            analysis.Status = "In Progress";

            await _context.ForensicAnalyses.InsertOneAsync(analysis);

            await _context.AuditLogs.InsertOneAsync(new AuditLog
            {
                UserId = user.Id,
                Action = "Start Analysis",
                EntityType = "ForensicAnalysis",
                EntityId = analysis.EvidenceId,
                Description = $"Forensic analysis started for Evidence ID {analysis.EvidenceId}"
            });

            await _context.Evidences.UpdateOneAsync(
                e => e.Id == analysis.EvidenceId,
                Builders<Evidence>.Update.Set(e => e.Status, "Under Analysis"));

            TempData["SuccessMessage"] = "Forensic analysis started.";
            return RedirectToAction("Details", "Evidences", new { id = analysis.EvidenceId });
        }
        return View(analysis);
    }

    [Authorize(Roles = "Forensic Analyst")]
    public async Task<IActionResult> Complete(string id)
    {
        var analysis = await _context.ForensicAnalyses.Find(f => f.Id == id).FirstOrDefaultAsync();
        if (analysis == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (analysis.AnalystId != user.Id && !User.IsInRole("Admin")) return Unauthorized();

        return View(analysis);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Forensic Analyst")]
    public async Task<IActionResult> Complete(string id, string findings, string notes)
    {
        var analysis = await _context.ForensicAnalyses.Find(a => a.Id == id).FirstOrDefaultAsync();
        if (analysis == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var update = Builders<ForensicAnalysis>.Update
            .Set(a => a.Findings, findings ?? analysis.Findings)
            .Set(a => a.Notes, notes ?? analysis.Notes)
            .Set(a => a.Status, "Completed")
            .Set(a => a.CompletedAt, DateTime.UtcNow);

        await _context.ForensicAnalyses.UpdateOneAsync(a => a.Id == id, update);

        await _context.Evidences.UpdateOneAsync(
            e => e.Id == analysis.EvidenceId,
            Builders<Evidence>.Update.Set(e => e.Status, "Analysis Completed"));

        await _context.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = user.Id,
            Action = "Complete Analysis",
            EntityType = "ForensicAnalysis",
            EntityId = analysis.Id,
            Description = "Forensic analysis completed for Evidence."
        });

        TempData["SuccessMessage"] = "Forensic analysis marked as completed.";
        return RedirectToAction("Details", "Evidences", new { id = analysis.EvidenceId });
    }
}
