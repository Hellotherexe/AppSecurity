using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookwormsOnline.Models;

/// <summary>
/// Password history entity for tracking previous passwords
/// </summary>
public class PasswordHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Member ID associated with this password history
    /// </summary>
    [Required]
    public int MemberId { get; set; }

    /// <summary>
    /// Hashed password (using PasswordHasher)
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this password was set
    /// </summary>
    [Required]
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("MemberId")]
    public virtual Member? Member { get; set; }
}
