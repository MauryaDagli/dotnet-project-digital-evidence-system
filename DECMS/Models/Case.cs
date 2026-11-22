using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DECMS.Models;

public class Case
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [Required]
    [StringLength(50)]
    public string CaseNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Open";

    [Required]
    [StringLength(50)]
    public string Priority { get; set; } = "Medium";

    public DateTime IncidentDate { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public string CreatedById { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? CreatedBy { get; set; }

    public string AssignedOfficerId { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? AssignedOfficer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    [BsonIgnore]
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
}
