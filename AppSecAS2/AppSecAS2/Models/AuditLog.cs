using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookwormsOnline.Models;

/// <summary>
/// Audit log entity for tracking member actions and security events
/// </summary>
public class AuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Member ID associated with the action (nullable for anonymous actions)
    /// </summary>
    public int? MemberId { get; set; }

    /// <summary>
    /// Action performed (e.g., LoginSuccess, LoginFailed, Logout, PasswordChanged)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the action in UTC
    /// </summary>
    [Required]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address from which the action was performed
    /// </summary>
    [StringLength(45)] // IPv6 max length
    public string? IPAddress { get; set; }

    /// <summary>
    /// User agent (browser/device information)
    /// </summary>
    [StringLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional details about the action
    /// </summary>
    [StringLength(1000)]
    public string? Details { get; set; }

    // Navigation property
    [ForeignKey("MemberId")]
    public virtual Member? Member { get; set; }
}
