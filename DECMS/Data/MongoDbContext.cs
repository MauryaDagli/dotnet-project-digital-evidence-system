using DECMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DECMS.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDb")
            ?? configuration["MongoDbSettings:ConnectionString"]
            ?? "mongodb://localhost:27017";

        var databaseName = configuration["MongoDbSettings:DatabaseName"] ?? "DECMS";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<ApplicationUser> Users => _database.GetCollection<ApplicationUser>("Users");
    public IMongoCollection<ApplicationRole> Roles => _database.GetCollection<ApplicationRole>("Roles");
    public IMongoCollection<Case> Cases => _database.GetCollection<Case>("Cases");
    public IMongoCollection<Evidence> Evidences => _database.GetCollection<Evidence>("Evidences");
    public IMongoCollection<CustodyRecord> CustodyRecords => _database.GetCollection<CustodyRecord>("CustodyRecords");
    public IMongoCollection<ForensicAnalysis> ForensicAnalyses => _database.GetCollection<ForensicAnalysis>("ForensicAnalyses");
    public IMongoCollection<AuditLog> AuditLogs => _database.GetCollection<AuditLog>("AuditLogs");

    public async Task<Case?> GetCaseWithDetailsAsync(string id)
    {
        var @case = await Cases.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (@case == null) return null;

        if (!string.IsNullOrEmpty(@case.CreatedById) && ObjectId.TryParse(@case.CreatedById, out _))
            @case.CreatedBy = await Users.Find(u => u.Id == @case.CreatedById).FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(@case.AssignedOfficerId) && ObjectId.TryParse(@case.AssignedOfficerId, out _))
            @case.AssignedOfficer = await Users.Find(u => u.Id == @case.AssignedOfficerId).FirstOrDefaultAsync();

        @case.Evidences = await Evidences.Find(e => e.CaseId == id).ToListAsync();
        foreach (var ev in @case.Evidences)
        {
            if (!string.IsNullOrEmpty(ev.CurrentCustodianId) && ObjectId.TryParse(ev.CurrentCustodianId, out _))
                ev.CurrentCustodian = await Users.Find(u => u.Id == ev.CurrentCustodianId).FirstOrDefaultAsync();
        }

        return @case;
    }

    public async Task<Evidence?> GetEvidenceWithDetailsAsync(string id)
    {
        var evidence = await Evidences.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (evidence == null) return null;

        if (!string.IsNullOrEmpty(evidence.CaseId) && ObjectId.TryParse(evidence.CaseId, out _))
            evidence.Case = await Cases.Find(c => c.Id == evidence.CaseId).FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(evidence.CollectedById) && ObjectId.TryParse(evidence.CollectedById, out _))
            evidence.CollectedBy = await Users.Find(u => u.Id == evidence.CollectedById).FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(evidence.CurrentCustodianId) && ObjectId.TryParse(evidence.CurrentCustodianId, out _))
            evidence.CurrentCustodian = await Users.Find(u => u.Id == evidence.CurrentCustodianId).FirstOrDefaultAsync();

        var records = await CustodyRecords.Find(cr => cr.EvidenceId == id)
            .SortByDescending(cr => cr.Timestamp)
            .ToListAsync();

        var userIds = records.Select(r => r.FromUserId).Concat(records.Select(r => r.ToUserId))
            .Where(uid => !string.IsNullOrEmpty(uid) && ObjectId.TryParse(uid, out _))
            .Distinct().ToList();
        var users = userIds.Count > 0
            ? (await Users.Find(u => userIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id)
            : new Dictionary<string, ApplicationUser>();

        foreach (var r in records)
        {
            if (users.TryGetValue(r.FromUserId, out var fromUser)) r.FromUser = fromUser;
            if (users.TryGetValue(r.ToUserId, out var toUser)) r.ToUser = toUser;
        }
        evidence.CustodyRecords = records;

        var analyses = await ForensicAnalyses.Find(fa => fa.EvidenceId == id).ToListAsync();
        var analystIds = analyses.Select(a => a.AnalystId)
            .Where(aid => !string.IsNullOrEmpty(aid) && ObjectId.TryParse(aid, out _))
            .Distinct().ToList();
        var analysts = analystIds.Count > 0
            ? (await Users.Find(u => analystIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id)
            : new Dictionary<string, ApplicationUser>();

        foreach (var a in analyses)
        {
            if (analysts.TryGetValue(a.AnalystId, out var analyst)) a.Analyst = analyst;
        }
        evidence.ForensicAnalyses = analyses;

        return evidence;
    }

    public async Task<List<Case>> GetCasesListAsync(string? searchString)
    {
        var filter = Builders<Case>.Filter.Empty;
        if (!string.IsNullOrEmpty(searchString))
        {
            var caseNumFilter = Builders<Case>.Filter.Regex(c => c.CaseNumber, new MongoDB.Bson.BsonRegularExpression(searchString, "i"));
            var titleFilter = Builders<Case>.Filter.Regex(c => c.Title, new MongoDB.Bson.BsonRegularExpression(searchString, "i"));
            filter = Builders<Case>.Filter.Or(caseNumFilter, titleFilter);
        }

        var cases = await Cases.Find(filter).SortByDescending(c => c.CreatedAt).ToListAsync();
        var userIds = cases.Select(c => c.CreatedById).Concat(cases.Select(c => c.AssignedOfficerId))
            .Where(uid => !string.IsNullOrEmpty(uid) && ObjectId.TryParse(uid, out _))
            .Distinct().ToList();
        var users = userIds.Count > 0
            ? (await Users.Find(u => userIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id)
            : new Dictionary<string, ApplicationUser>();

        foreach (var c in cases)
        {
            if (users.TryGetValue(c.CreatedById, out var creator)) c.CreatedBy = creator;
            if (users.TryGetValue(c.AssignedOfficerId, out var assigned)) c.AssignedOfficer = assigned;
        }

        return cases;
    }

    public async Task<List<Evidence>> GetEvidencesListAsync(string? searchString)
    {
        var filter = Builders<Evidence>.Filter.Empty;
        if (!string.IsNullOrEmpty(searchString))
        {
            var evNumFilter = Builders<Evidence>.Filter.Regex(e => e.EvidenceNumber, new MongoDB.Bson.BsonRegularExpression(searchString, "i"));
            var nameFilter = Builders<Evidence>.Filter.Regex(e => e.Name, new MongoDB.Bson.BsonRegularExpression(searchString, "i"));
            filter = Builders<Evidence>.Filter.Or(evNumFilter, nameFilter);
        }

        var evidences = await Evidences.Find(filter).SortByDescending(e => e.CreatedAt).ToListAsync();
        var caseIds = evidences.Select(e => e.CaseId)
            .Where(cid => !string.IsNullOrEmpty(cid) && ObjectId.TryParse(cid, out _))
            .Distinct().ToList();
        var cases = caseIds.Count > 0
            ? (await Cases.Find(c => caseIds.Contains(c.Id)).ToListAsync()).ToDictionary(c => c.Id)
            : new Dictionary<string, Case>();

        var custodianIds = evidences.Select(e => e.CurrentCustodianId)
            .Where(uid => !string.IsNullOrEmpty(uid) && ObjectId.TryParse(uid, out _))
            .Distinct().ToList();
        var users = custodianIds.Count > 0
            ? (await Users.Find(u => custodianIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id)
            : new Dictionary<string, ApplicationUser>();

        foreach (var e in evidences)
        {
            if (cases.TryGetValue(e.CaseId, out var c)) e.Case = c;
            if (users.TryGetValue(e.CurrentCustodianId, out var custodian)) e.CurrentCustodian = custodian;
        }

        return evidences;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync()
    {
        var totalCases = (int)await Cases.CountDocumentsAsync(Builders<Case>.Filter.Empty);
        var activeCases = (int)await Cases.CountDocumentsAsync(c => c.Status != "Closed" && c.Status != "Archived");
        var closedCases = (int)await Cases.CountDocumentsAsync(c => c.Status == "Closed");
        var totalEvidence = (int)await Evidences.CountDocumentsAsync(Builders<Evidence>.Filter.Empty);
        var pendingAnalysis = (int)await ForensicAnalyses.CountDocumentsAsync(fa => fa.Status == "Pending" || fa.Status == "In Progress");

        var recentCustody = await CustodyRecords.Find(Builders<CustodyRecord>.Filter.Empty)
            .SortByDescending(cr => cr.Timestamp)
            .Limit(5)
            .ToListAsync();

        var evIds = recentCustody.Select(r => r.EvidenceId)
            .Where(eid => !string.IsNullOrEmpty(eid) && ObjectId.TryParse(eid, out _))
            .Distinct().ToList();
        var evidences = evIds.Count > 0
            ? (await Evidences.Find(e => evIds.Contains(e.Id)).ToListAsync()).ToDictionary(e => e.Id)
            : new Dictionary<string, Evidence>();

        var userIds = recentCustody.Select(r => r.FromUserId).Concat(recentCustody.Select(r => r.ToUserId))
            .Where(uid => !string.IsNullOrEmpty(uid) && ObjectId.TryParse(uid, out _))
            .Distinct().ToList();
        var users = userIds.Count > 0
            ? (await Users.Find(u => userIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id)
            : new Dictionary<string, ApplicationUser>();

        foreach (var r in recentCustody)
        {
            if (evidences.TryGetValue(r.EvidenceId, out var ev)) r.Evidence = ev;
            if (users.TryGetValue(r.FromUserId, out var f)) r.FromUser = f;
            if (users.TryGetValue(r.ToUserId, out var t)) r.ToUser = t;
        }

        return new DashboardViewModel
        {
            TotalCases = totalCases,
            ActiveCases = activeCases,
            ClosedCases = closedCases,
            TotalEvidence = totalEvidence,
            EvidencePendingAnalysis = pendingAnalysis,
            RecentCustodyTransfers = recentCustody
        };
    }
}
