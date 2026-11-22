using DECMS.Data;
using DECMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DECMS.Controllers;

[Authorize(Roles = "Admin,Court Officer,Police Officer")]
public class AuditLogsController : Controller
{
    private readonly MongoDbContext _context;

    public AuditLogsController(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var logs = await _context.AuditLogs
            .Find(Builders<AuditLog>.Filter.Empty)
            .SortByDescending(a => a.Timestamp)
            .Limit(200)
            .ToListAsync();

        var usersList = await _context.Users
            .Find(Builders<ApplicationUser>.Filter.Empty)
            .ToListAsync();

        var users = usersList.ToDictionary(u => u.Id, u => u.FullName);
        ViewBag.Users = users;

        return View(logs);
    }
}
