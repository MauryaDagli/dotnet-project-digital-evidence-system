using DECMS.Models;
using DECMS.Utilities;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;

namespace DECMS.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        string[] roles = { "Admin", "Police Officer", "Investigator", "Forensic Analyst", "Court Officer" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        var demoUsers = new List<(string Email, string Name, string Role, string Dept)>
        {
            ("admin@decms.local", "System Administrator", "Admin", "IT & Security Operations"),
            ("officer@decms.local", "Officer John Doe", "Police Officer", "Cybercrime First Response"),
            ("investigator@decms.local", "Investigator Jane Smith", "Investigator", "Digital Forensics & Financial Crimes"),
            ("forensic@decms.local", "Dr. Alan Grant", "Forensic Analyst", "Forensics Laboratory"),
            ("court@decms.local", "Judge Judy", "Court Officer", "Judiciary Court Division")
        };

        foreach (var (Email, Name, Role, Dept) in demoUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(Email);
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = Email,
                    Email = Email,
                    FullName = Name,
                    Department = Dept,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, "Demo@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, Role);
                }
            }
        }

        // Seed Realistic Demo Cases and Evidence if MongoDB is empty
        long caseCount = await mongoContext.Cases.CountDocumentsAsync(Builders<Case>.Filter.Empty);
        if (caseCount == 0)
        {
            var officer = await userManager.FindByEmailAsync("officer@decms.local");
            var investigator = await userManager.FindByEmailAsync("investigator@decms.local");
            var forensic = await userManager.FindByEmailAsync("forensic@decms.local");
            var admin = await userManager.FindByEmailAsync("admin@decms.local");

            if (officer != null && investigator != null && forensic != null && admin != null)
            {
                string uploadsFolder = Path.Combine(env.ContentRootPath, "SecureUploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Create Case 1
                var case1 = new Case
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CaseNumber = "CASE-2026-0001",
                    Title = "Apex National Bank Wire Fraud & Unauthorized Access",
                    Description = "Suspected internal network compromise leading to unauthorized wire transfers totaling $480,000. Suspicious gateway IP routing detected.",
                    Status = "Under Investigation",
                    Priority = "High",
                    IncidentDate = DateTime.UtcNow.AddDays(-12),
                    Location = "Apex Financial Tower, 4th Floor Server Room",
                    CreatedById = officer.Id,
                    AssignedOfficerId = investigator.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-11)
                };

                // Create Case 2
                var case2 = new Case
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CaseNumber = "CASE-2026-0002",
                    Title = "Municipal Infrastructure SCADA Intrusion",
                    Description = "Ransomware staging and unauthorized credential harvesting attempts flagged across water treatment telemetry controllers.",
                    Status = "Forensic Analysis",
                    Priority = "High",
                    IncidentDate = DateTime.UtcNow.AddDays(-7),
                    Location = "Central Water Works Facility, Sector 9",
                    CreatedById = officer.Id,
                    AssignedOfficerId = investigator.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-6)
                };

                // Create Case 3
                var case3 = new Case
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CaseNumber = "CASE-2026-0003",
                    Title = "Corporate IP Theft & Executive Mailbox Exfiltration",
                    Description = "Suspected insider leakage of confidential R&D blueprints ahead of public patent filing.",
                    Status = "Open",
                    Priority = "Medium",
                    IncidentDate = DateTime.UtcNow.AddDays(-3),
                    Location = "Novatech Headquarters, Suite 1200",
                    CreatedById = officer.Id,
                    AssignedOfficerId = officer.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };

                await mongoContext.Cases.InsertManyAsync(new[] { case1, case2, case3 });

                // Generate Sample Evidence Files with actual SHA-256 hashes
                string file1Name = $"{Guid.NewGuid()}_apex_transactions.csv";
                string file1Path = Path.Combine(uploadsFolder, file1Name);
                string file1Content = "timestamp,source_ip,destination_account,amount_usd,status\n" +
                                     "2026-11-10T02:14:22Z,198.51.100.42,ACC-99210-KYC,150000,AUTHORIZED_BYPASSED\n" +
                                     "2026-11-10T02:19:04Z,198.51.100.42,ACC-88319-SWIFT,330000,AUTHORIZED_BYPASSED\n";
                await File.WriteAllTextAsync(file1Path, file1Content, Encoding.UTF8);
                string hash1 = HashHelper.CalculateSHA256(file1Path);

                var evidence1 = new Evidence
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CaseId = case1.Id,
                    EvidenceNumber = "EVD-2026-0001",
                    Name = "Apex Gateway Audit Transaction Dump",
                    Description = "Exported transaction routing log extracted directly from Core Banking Gateway Node 02 during incident triage.",
                    Type = "Log File",
                    OriginalFileName = "apex_transactions.csv",
                    StoredFileName = file1Name,
                    FilePath = file1Path,
                    MimeType = "text/csv",
                    FileSize = new FileInfo(file1Path).Length,
                    SHA256Hash = hash1,
                    CollectionDate = DateTime.UtcNow.AddDays(-11),
                    CollectionLocation = "Apex Server Room - Rack B2",
                    CollectedById = officer.Id,
                    CurrentCustodianId = forensic.Id, // currently with Dr. Alan Grant
                    Status = "Under Analysis",
                    CreatedAt = DateTime.UtcNow.AddDays(-11)
                };

                string file2Name = $"{Guid.NewGuid()}_scada_packet_capture.pcap";
                string file2Path = Path.Combine(uploadsFolder, file2Name);
                byte[] samplePcapBytes = Encoding.UTF8.GetBytes("[PCAP_RAW_ETHERNET_FRAME_CAPTURE: PROTOCOL=MODBUS/TCP PORT=502 CMD=WRITE_SINGLE_COIL]");
                await File.WriteAllBytesAsync(file2Path, samplePcapBytes);
                string hash2 = HashHelper.CalculateSHA256(file2Path);

                var evidence2 = new Evidence
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CaseId = case2.Id,
                    EvidenceNumber = "EVD-2026-0002",
                    Name = "SCADA Telemetry Network Capture",
                    Description = "Raw packet capture of ingress and egress traffic captured over port 502 across the water distribution subnetwork.",
                    Type = "Log File",
                    OriginalFileName = "scada_packet_capture.pcap",
                    StoredFileName = file2Name,
                    FilePath = file2Path,
                    MimeType = "application/vnd.tcpdump.pcap",
                    FileSize = samplePcapBytes.Length,
                    SHA256Hash = hash2,
                    CollectionDate = DateTime.UtcNow.AddDays(-6),
                    CollectionLocation = "Central Water Works Substation A",
                    CollectedById = officer.Id,
                    CurrentCustodianId = investigator.Id,
                    Status = "In Custody",
                    CreatedAt = DateTime.UtcNow.AddDays(-6)
                };

                await mongoContext.Evidences.InsertManyAsync(new[] { evidence1, evidence2 });

                // Chain of Custody for Evidence 1
                var custody1_1 = new CustodyRecord
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence1.Id,
                    FromUserId = officer.Id,
                    ToUserId = officer.Id,
                    Action = "Evidence Collected & Registered",
                    Reason = "Secured directly from banking server hardware via write-blocker adapter.",
                    Location = "Apex Financial Tower, 4th Floor Server Room",
                    Timestamp = DateTime.UtcNow.AddDays(-11),
                    Notes = "Initial forensic extraction. SHA-256 calculated immediately upon secure ingestion."
                };

                var custody1_2 = new CustodyRecord
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence1.Id,
                    FromUserId = officer.Id,
                    ToUserId = investigator.Id,
                    Action = "Custody Transferred",
                    Reason = "Handed over to Lead Cyber Investigator for case analysis and correlation.",
                    Location = "Metropolitan Police Precinct - Cyber Evidence Intake Room",
                    Timestamp = DateTime.UtcNow.AddDays(-10),
                    Notes = "Physical custody and digital files transferred with chain-of-custody slip."
                };

                var custody1_3 = new CustodyRecord
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence1.Id,
                    FromUserId = investigator.Id,
                    ToUserId = forensic.Id,
                    Action = "Submitted for Forensic Analysis",
                    Reason = "Requesting deep forensic packet tracing and anomaly verification.",
                    Location = "State Forensic Laboratory - Evidence Intake Counter",
                    Timestamp = DateTime.UtcNow.AddDays(-8),
                    Notes = "Assigned to Senior Analyst Dr. Alan Grant for analysis."
                };

                // Chain of Custody for Evidence 2
                var custody2_1 = new CustodyRecord
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence2.Id,
                    FromUserId = officer.Id,
                    ToUserId = officer.Id,
                    Action = "Evidence Collected & Registered",
                    Reason = "Network interface TAP recording saved to encrypted flash drive.",
                    Location = "Water Works Substation A",
                    Timestamp = DateTime.UtcNow.AddDays(-6),
                    Notes = "Hardware write-blocker utilized."
                };

                var custody2_2 = new CustodyRecord
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence2.Id,
                    FromUserId = officer.Id,
                    ToUserId = investigator.Id,
                    Action = "Custody Transferred",
                    Reason = "Transferred to Investigator for case integration.",
                    Location = "Investigation Dept Headquarters",
                    Timestamp = DateTime.UtcNow.AddDays(-5),
                    Notes = "Sealed evidence storage envelope verified intact."
                };

                await mongoContext.CustodyRecords.InsertManyAsync(new[] { custody1_1, custody1_2, custody1_3, custody2_1, custody2_2 });

                // Forensic Analysis for Evidence 1
                var analysis1 = new ForensicAnalysis
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    EvidenceId = evidence1.Id,
                    AnalystId = forensic.Id,
                    AnalysisType = "Network Log Analysis & Anomaly Detection",
                    Methodology = "ISO/IEC 27037 Digital Evidence Handling; automated regex parsing coupled with geo-IP lookup.",
                    ToolsUsed = "Wireshark 4.2, LogParser, Python 3.12, SHA-256 Verification Utility",
                    Findings = "Extracted logs confirm two unauthorized API wire requests originating from TOR exit-node 198.51.100.42. Bypassed two-factor token utilizing hijacked session cookie #88A3F.",
                    Status = "In Progress",
                    StartedAt = DateTime.UtcNow.AddDays(-8),
                    Notes = "Hash re-verified prior to analysis: Hash verified matching initial ingestion record."
                };
                await mongoContext.ForensicAnalyses.InsertOneAsync(analysis1);

                // Audit Logs
                var auditEntries = new List<AuditLog>
                {
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = officer.Id,
                        Action = "Create Case",
                        EntityType = "Case",
                        EntityId = case1.CaseNumber,
                        Description = $"Case {case1.CaseNumber} registered: '{case1.Title}'",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-11),
                        Success = true
                    },
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = officer.Id,
                        Action = "Register Evidence",
                        EntityType = "Evidence",
                        EntityId = evidence1.EvidenceNumber,
                        Description = $"Evidence {evidence1.EvidenceNumber} registered with SHA-256: {hash1}",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-11),
                        Success = true
                    },
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = officer.Id,
                        Action = "Transfer Evidence",
                        EntityType = "Evidence",
                        EntityId = evidence1.EvidenceNumber,
                        Description = $"Custody transferred from {officer.FullName} to {investigator.FullName}",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-10),
                        Success = true
                    },
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = investigator.Id,
                        Action = "Transfer Evidence",
                        EntityType = "Evidence",
                        EntityId = evidence1.EvidenceNumber,
                        Description = $"Custody transferred from {investigator.FullName} to {forensic.FullName} for forensic review",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-8),
                        Success = true
                    },
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = forensic.Id,
                        Action = "Verify Integrity",
                        EntityType = "Evidence",
                        EntityId = evidence1.EvidenceNumber,
                        Description = $"Integrity verification executed. Result: VERIFIED. Hash matches: {hash1}",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-8),
                        Success = true
                    },
                    new AuditLog
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = forensic.Id,
                        Action = "Start Analysis",
                        EntityType = "ForensicAnalysis",
                        EntityId = evidence1.EvidenceNumber,
                        Description = "Forensic analysis procedure initiated by Dr. Alan Grant",
                        IpAddress = "127.0.0.1",
                        Timestamp = DateTime.UtcNow.AddDays(-8),
                        Success = true
                    }
                };

                await mongoContext.AuditLogs.InsertManyAsync(auditEntries);
            }
        }
    }
}
