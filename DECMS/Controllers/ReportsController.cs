using DECMS.Data;
using DECMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DECMS.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly MongoDbContext _context;

    public ReportsController(MongoDbContext context)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        _context = context;
    }

    public async Task<IActionResult> CaseReport(string id)
    {
        var @case = await _context.GetCaseWithDetailsAsync(id);
        if (@case == null) return NotFound();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(compose => ComposeHeader(compose, @case));
                page.Content().Element(compose => ComposeContent(compose, @case));
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = document.GeneratePdf();

        await _context.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
            Action = "Generate Report",
            EntityType = "Case",
            EntityId = @case.CaseNumber,
            Description = "Generated PDF report for case."
        });

        return File(pdfBytes, "application/pdf", $"CaseReport_{@case.CaseNumber}.pdf");
    }

    void ComposeHeader(IContainer container, Case @case)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("DIGITAL EVIDENCE CHAIN MANAGEMENT SYSTEM").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text("OFFICIAL CASE REPORT (MONGODB BACKED)").FontSize(14).SemiBold();
                column.Item().PaddingTop(5).Text($"Case Number: {@case.CaseNumber}").FontSize(12);
                column.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });
    }

    void ComposeContent(IContainer container, Case @case)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);

            column.Item().Text("1. Case Details").FontSize(14).SemiBold().Underline();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100);
                    columns.RelativeColumn();
                });

                table.Cell().Text("Title:"); table.Cell().Text(@case.Title);
                table.Cell().Text("Status:"); table.Cell().Text(@case.Status);
                table.Cell().Text("Priority:"); table.Cell().Text(@case.Priority);
                table.Cell().Text("Incident Date:"); table.Cell().Text(@case.IncidentDate.ToString("yyyy-MM-dd"));
                table.Cell().Text("Location:"); table.Cell().Text(@case.Location);
                table.Cell().Text("Assigned To:"); table.Cell().Text(@case.AssignedOfficer?.FullName ?? "N/A");
            });

            column.Item().Text("2. Description").FontSize(14).SemiBold().Underline();
            column.Item().Text(@case.Description);

            column.Item().Text($"3. Registered Evidence ({@case.Evidences.Count})").FontSize(14).SemiBold().Underline();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).PaddingBottom(5).Text("Evidence ID").SemiBold();
                    header.Cell().BorderBottom(1).PaddingBottom(5).Text("Name").SemiBold();
                    header.Cell().BorderBottom(1).PaddingBottom(5).Text("Type").SemiBold();
                    header.Cell().BorderBottom(1).PaddingBottom(5).Text("Custodian").SemiBold();
                });

                foreach (var ev in @case.Evidences)
                {
                    table.Cell().PaddingVertical(5).Text(ev.EvidenceNumber);
                    table.Cell().PaddingVertical(5).Text(ev.Name);
                    table.Cell().PaddingVertical(5).Text(ev.Type);
                    table.Cell().PaddingVertical(5).Text(ev.CurrentCustodian?.FullName ?? "N/A");
                }
            });
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }

    public async Task<IActionResult> EvidenceReport(string id)
    {
        var evidence = await _context.GetEvidenceWithDetailsAsync(id);
        if (evidence == null) return NotFound();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(compose =>
                {
                    compose.Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("DIGITAL EVIDENCE CHAIN MANAGEMENT SYSTEM").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                            column.Item().Text("EVIDENCE & CHAIN OF CUSTODY REPORT").FontSize(14).SemiBold();
                            column.Item().PaddingTop(5).Text($"Evidence ID: {evidence.EvidenceNumber}").FontSize(12);
                            column.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    });
                });

                page.Content().Element(compose =>
                {
                    compose.PaddingVertical(1, Unit.Centimetre).Column(column =>
                    {
                        column.Spacing(15);

                        column.Item().Text("1. Evidence Identity & Metadata").FontSize(12).SemiBold().Underline();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(120); columns.RelativeColumn(); });
                            table.Cell().Text("Name:"); table.Cell().Text(evidence.Name);
                            table.Cell().Text("Case:"); table.Cell().Text(evidence.Case?.CaseNumber ?? "N/A");
                            table.Cell().Text("Type:"); table.Cell().Text(evidence.Type);
                            table.Cell().Text("Status:"); table.Cell().Text(evidence.Status);
                            table.Cell().Text("Collection Date:"); table.Cell().Text(evidence.CollectionDate.ToString("yyyy-MM-dd HH:mm"));
                            table.Cell().Text("Location:"); table.Cell().Text(evidence.CollectionLocation);
                            table.Cell().Text("Collected By:"); table.Cell().Text(evidence.CollectedBy?.FullName ?? "N/A");
                            table.Cell().Text("Current Custodian:"); table.Cell().Text(evidence.CurrentCustodian?.FullName ?? "N/A");
                        });

                        column.Item().Text("2. Digital Integrity").FontSize(12).SemiBold().Underline();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(120); columns.RelativeColumn(); });
                            table.Cell().Text("Original File:"); table.Cell().Text(evidence.OriginalFileName);
                            table.Cell().Text("Size:"); table.Cell().Text($"{(evidence.FileSize / 1024)} KB");
                            table.Cell().Text("MIME:"); table.Cell().Text(evidence.MimeType);
                            table.Cell().Text("SHA-256 Hash:"); table.Cell().Text(evidence.SHA256Hash).FontSize(8);
                        });

                        column.Item().Text($"3. Chain of Custody History ({evidence.CustodyRecords.Count} records)").FontSize(12).SemiBold().Underline();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(100);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).PaddingBottom(2).Text("Timestamp").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(2).Text("Action").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(2).Text("From").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(2).Text("To").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(2).Text("Reason").SemiBold();
                            });
                            foreach (var cr in evidence.CustodyRecords.OrderBy(r => r.Timestamp))
                            {
                                table.Cell().PaddingVertical(3).Text(cr.Timestamp.ToString("yyyy-MM-dd HH:mm"));
                                table.Cell().PaddingVertical(3).Text(cr.Action);
                                table.Cell().PaddingVertical(3).Text(cr.FromUser?.FullName ?? "System");
                                table.Cell().PaddingVertical(3).Text(cr.ToUser?.FullName ?? "System");
                                table.Cell().PaddingVertical(3).Text(cr.Reason);
                            }
                        });

                        if (evidence.ForensicAnalyses.Any())
                        {
                            column.Item().Text("4. Forensic Analysis").FontSize(12).SemiBold().Underline();
                            foreach (var analysis in evidence.ForensicAnalyses)
                            {
                                column.Item().Text($"- {analysis.AnalysisType} ({analysis.Status})").SemiBold();
                                column.Item().Text($"  Findings: {analysis.Findings}");
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages(); });
            });
        });

        var pdfBytes = document.GeneratePdf();

        await _context.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
            Action = "Generate Report",
            EntityType = "Evidence",
            EntityId = evidence.EvidenceNumber,
            Description = "Generated PDF report for evidence."
        });

        return File(pdfBytes, "application/pdf", $"EvidenceReport_{evidence.EvidenceNumber}.pdf");
    }
}
