using DECMS.Data;
using DECMS.Models;
using DECMS.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;

namespace DECMS.Controllers;

[Authorize]
public class EvidencesController : Controller
{
    private readonly MongoDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public EvidencesController(MongoDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
    }

    public async Task<IActionResult> Index(string searchString)
    {
        ViewData["CurrentFilter"] = searchString;
        var evidences = await _context.GetEvidencesListAsync(searchString);
        return View(evidences);
    }

    public async Task<IActionResult> Details(string? id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var evidence = await _context.GetEvidenceWithDetailsAsync(id);
        if (evidence == null) return NotFound();

        return View(evidence);
    }

    [Authorize(Roles = "Admin,Police Officer,Investigator")]
    public async Task<IActionResult> Create(string? caseId)
    {
        var cases = await _context.Cases.Find(Builders<Case>.Filter.Empty).SortByDescending(c => c.CreatedAt).ToListAsync();
        ViewBag.Cases = new SelectList(cases.Select(c => new { Id = c.Id, Display = $"{c.CaseNumber} - {c.Title}" }), "Id", "Display", caseId);
        ViewBag.CaseId = caseId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Police Officer,Investigator")]
    public async Task<IActionResult> Create(string CaseId, [Bind("EvidenceNumber,Name,Description,Type,CollectionDate,CollectionLocation")] Evidence evidence, IFormFile? evidenceFile)
    {
        if (string.IsNullOrWhiteSpace(evidence.EvidenceNumber))
        {
            var count = await _context.Evidences.CountDocumentsAsync(Builders<Evidence>.Filter.Empty);
            evidence.EvidenceNumber = $"EVD-{DateTime.UtcNow.Year}-{(count + 1):D4}";
            ModelState.Remove("EvidenceNumber");
        }

        if (string.IsNullOrEmpty(CaseId))
        {
            ModelState.AddModelError("CaseId", "Please select an associated case.");
        }

        if (evidenceFile == null || evidenceFile.Length == 0)
        {
            ModelState.AddModelError("evidenceFile", "Please upload an evidence file.");
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Handle file upload
            string uploadsFolder = Path.Combine(_env.ContentRootPath, "SecureUploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(evidenceFile!.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await evidenceFile.CopyToAsync(fileStream);
            }

            // Set evidence properties
            evidence.CaseId = CaseId;
            evidence.OriginalFileName = evidenceFile.FileName;
            evidence.StoredFileName = uniqueFileName;
            evidence.FilePath = filePath;
            evidence.MimeType = evidenceFile.ContentType;
            evidence.FileSize = evidenceFile.Length;
            evidence.SHA256Hash = HashHelper.CalculateSHA256(filePath);
            evidence.CollectedById = user.Id;
            evidence.CurrentCustodianId = user.Id;
            evidence.CreatedAt = DateTime.UtcNow;

            await _context.Evidences.InsertOneAsync(evidence);

            // Add initial custody record
            var custodyRecord = new CustodyRecord
            {
                EvidenceId = evidence.Id,
                FromUserId = user.Id,
                ToUserId = user.Id,
                Action = "Evidence Registered and Uploaded",
                Reason = "Initial Registration",
                Location = evidence.CollectionLocation,
                Timestamp = DateTime.UtcNow,
                Notes = "Evidence hash calculated automatically upon upload."
            };
            await _context.CustodyRecords.InsertOneAsync(custodyRecord);

            // Audit Log
            await _context.AuditLogs.InsertOneAsync(new AuditLog
            {
                UserId = user.Id,
                Action = "Register Evidence",
                EntityType = "Evidence",
                EntityId = evidence.EvidenceNumber,
                Description = $"Evidence {evidence.EvidenceNumber} registered with hash {evidence.SHA256Hash} in MongoDB."
            });

            TempData["SuccessMessage"] = "Evidence successfully registered and secured.";
            return RedirectToAction("Details", new { id = evidence.Id });
        }

        var allCases = await _context.Cases.Find(Builders<Case>.Filter.Empty).SortByDescending(c => c.CreatedAt).ToListAsync();
        ViewBag.Cases = new SelectList(allCases.Select(c => new { Id = c.Id, Display = $"{c.CaseNumber} - {c.Title}" }), "Id", "Display", CaseId);
        ViewBag.CaseId = CaseId;
        return View(evidence);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> VerifyIntegrity(string id)
    {
        var evidence = await _context.Evidences.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (evidence == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        string userId = user?.Id ?? "Unknown";

        if (!System.IO.File.Exists(evidence.FilePath))
        {
            TempData["ErrorMessage"] = "Evidence file is missing from secure storage!";
            await _context.AuditLogs.InsertOneAsync(new AuditLog
            {
                UserId = userId,
                Action = "Verify Integrity",
                EntityType = "Evidence",
                EntityId = evidence.EvidenceNumber,
                Description = "File missing",
                Success = false
            });
            return RedirectToAction(nameof(Details), new { id });
        }

        string currentHash = HashHelper.CalculateSHA256(evidence.FilePath);
        bool isValid = string.Equals(currentHash, evidence.SHA256Hash, StringComparison.OrdinalIgnoreCase);

        await _context.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            Action = "Verify Integrity",
            EntityType = "Evidence",
            EntityId = evidence.EvidenceNumber,
            Description = $"Integrity verification. Result: {(isValid ? "VERIFIED" : "COMPROMISED")}. Original Hash: {evidence.SHA256Hash}, Current Hash: {currentHash}",
            Success = isValid
        });

        if (isValid)
            TempData["SuccessMessage"] = $"INTEGRITY VERIFIED. The file matches the original SHA-256 hash: {currentHash}";
        else
            TempData["ErrorMessage"] = $"INTEGRITY COMPROMISED! Original Hash: {evidence.SHA256Hash} | Current Hash: {currentHash}";

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,Investigator,Forensic Analyst")]
    public async Task<IActionResult> Download(string id)
    {
        var evidence = await _context.Evidences.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (evidence == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        string userId = user?.Id ?? "Unknown";

        await _context.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            Action = "Download Evidence",
            EntityType = "Evidence",
            EntityId = evidence.EvidenceNumber,
            Description = $"User downloaded evidence file {evidence.OriginalFileName}"
        });

        if (!System.IO.File.Exists(evidence.FilePath))
            return NotFound("File not found on server.");

        return PhysicalFile(evidence.FilePath, evidence.MimeType, evidence.OriginalFileName);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Police Officer,Investigator")]
    public async Task<IActionResult> Transfer(string id)
    {
        var evidence = await _context.GetEvidenceWithDetailsAsync(id);
        if (evidence == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (evidence.CurrentCustodianId != user.Id && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Only the current custodian or Admin can transfer this evidence.";
            return RedirectToAction("Details", new { id });
        }

        var allUsers = await _context.Users.Find(Builders<ApplicationUser>.Filter.Empty).ToListAsync();
        ViewBag.ToUserId = new SelectList(allUsers.Where(u => u.Id != user.Id), "Id", "FullName");

        return View(evidence);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Police Officer,Investigator")]
    public async Task<IActionResult> Transfer(string id, string ToUserId, string Reason, string Notes, string Location)
    {
        var evidence = await _context.Evidences.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (evidence == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        if (evidence.CurrentCustodianId != currentUser.Id && !User.IsInRole("Admin"))
        {
            return Unauthorized();
        }

        var toUser = await _userManager.FindByIdAsync(ToUserId);
        if (toUser == null) return BadRequest("Invalid destination user.");

        try
        {
            // Create Custody Record
            var custodyRecord = new CustodyRecord
            {
                EvidenceId = evidence.Id,
                FromUserId = currentUser.Id,
                ToUserId = toUser.Id,
                Action = "Evidence Transferred",
                Reason = Reason,
                Location = Location ?? "Precinct Evidence Locker",
                Notes = Notes ?? "",
                Timestamp = DateTime.UtcNow
            };
            await _context.CustodyRecords.InsertOneAsync(custodyRecord);

            // Update Evidence Custodian
            var update = Builders<Evidence>.Update
                .Set(e => e.CurrentCustodianId, toUser.Id)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);
            await _context.Evidences.UpdateOneAsync(e => e.Id == id, update);

            // Audit Log
            await _context.AuditLogs.InsertOneAsync(new AuditLog
            {
                UserId = currentUser.Id,
                Action = "Transfer Evidence",
                EntityType = "Evidence",
                EntityId = evidence.EvidenceNumber,
                Description = $"Transferred from {currentUser.FullName} to {toUser.FullName}. Reason: {Reason}"
            });

            TempData["SuccessMessage"] = $"Evidence successfully transferred to {toUser.FullName}.";
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred during transfer: {ex.Message}";
            return RedirectToAction("Details", new { id });
        }
    }

    [AllowAnonymous]
    public IActionResult GenerateQRCode(string evidenceNumber)
    {
        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode($"DECMS:EVIDENCE:{evidenceNumber}", QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        byte[] qrCodeImage = qrCode.GetGraphic(20);
        return File(qrCodeImage, "image/png");
    }
}
