using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DECMS.Models;

public class CustodyRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string EvidenceId { get; set; } = string.Empty;

    [BsonIgnore]
    public Evidence? Evidence { get; set; }

    public string FromUserId { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? FromUser { get; set; }

    public string ToUserId { get; set; } = string.Empty;

    [BsonIgnore]
    public ApplicationUser? ToUser { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    public string Reason { get; set; } = string.Empty;

    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string Notes { get; set; } = string.Empty;
}
