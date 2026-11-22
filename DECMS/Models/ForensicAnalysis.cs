using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DECMS.Models;

public class ForensicAnalysis
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string EvidenceId { get; set; } = string.Empty;

    [BsonIgnore]
    public Evidence? Evidence { get; set; }

    public string AnalystId { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? Analyst { get; set; }

    [Required]
    [StringLength(100)]
    public string AnalysisType { get; set; } = string.Empty;

    public string Methodology { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
    public string ToolsUsed { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string Notes { get; set; } = string.Empty;
}
