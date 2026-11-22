using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DECMS.Models;

public class Evidence
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [Required]
    [StringLength(50)]
    public string EvidenceNumber { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    [BsonIgnore]
    public Case? Case { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    [Required]
    [StringLength(64)]
    public string SHA256Hash { get; set; } = string.Empty;

    public DateTime CollectionDate { get; set; } = DateTime.UtcNow;
    public string CollectionLocation { get; set; } = string.Empty;
    public string CollectedById { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? CollectedBy { get; set; }

    public string CurrentCustodianId { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? CurrentCustodian { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Registered";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [BsonIgnore]
    public ICollection<CustodyRecord> CustodyRecords { get; set; } = new List<CustodyRecord>();

    [BsonIgnore]
    public ICollection<ForensicAnalysis> ForensicAnalyses { get; set; } = new List<ForensicAnalysis>();
}
