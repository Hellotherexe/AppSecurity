using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookwormsOnline.Models;

/// <summary>
/// Password reset token entity for forgot password flow
/// </summary>
public class PasswordResetToken
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Member ID associated with this reset token
    /// </summary>
    [Required]
    public int MemberId { get; set; }

    /// <summary>
    /// Secure random token (GUID or similar)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this token was created
    /// </summary>
    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when this token expires (typically 1-24 hours)
    /// </summary>
    [Required]
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether this token has been used
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// UTC timestamp when this token was used (if applicable)
    /// </summary>
    public DateTime? UsedAtUtc { get; set; }

    // Navigation property
    [ForeignKey("MemberId")]
    public virtual Member? Member { get; set; }
}
