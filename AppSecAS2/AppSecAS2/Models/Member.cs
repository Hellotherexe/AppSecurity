using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookwormsOnline.Models;

public class Member
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MemberId { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Credit Card (Encrypted)")]
    public string CreditCardEncrypted { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required")]
    [RegularExpression(@"^[89]\d{7}$", ErrorMessage = "Mobile number must be a valid Singapore mobile number (8 or 9 followed by 7 digits)")]
    [Display(Name = "Mobile Number")]
    public string MobileNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Billing address is required")]
    [StringLength(255, ErrorMessage = "Billing address cannot exceed 255 characters")]
    [Display(Name = "Billing Address")]
    public string BillingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Shipping address is required")]
    [StringLength(500, ErrorMessage = "Shipping address cannot exceed 500 characters")]
    [Display(Name = "Shipping Address")]
    [RegularExpression(@"^[a-zA-Z0-9\s\.,#\-/()]+$", 
        ErrorMessage = "Shipping address contains invalid characters. Only letters, numbers, spaces, and common punctuation (.,#-/()) are allowed.")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    [RegularExpression(@"^.*\.(jpg|JPG)$", ErrorMessage = "Only JPG files are allowed")]
    [Display(Name = "Photo File Name")]
    public string PhotoFileName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Last Login At")]
    public DateTime? LastLoginAt { get; set; }

    // Account lockout fields
    [Display(Name = "Failed Login Count")]
    public int FailedLoginCount { get; set; } = 0;

    [Display(Name = "Lockout End")]
    public DateTime? LockoutEndUtc { get; set; }

    // Session management
    [StringLength(100)]
    [Display(Name = "Current Session ID")]
    public string? CurrentSessionId { get; set; }

    // Password management
    [Display(Name = "Password Changed At")]
    public DateTime? PasswordChangedAtUtc { get; set; }

    // Two-Factor Authentication (2FA)
    [Display(Name = "2FA Enabled")]
    public bool TwoFactorEnabled { get; set; } = false;

    [Display(Name = "2FA Type")]
    [StringLength(20)]
    public string? TwoFactorType { get; set; } // "Email" or "TOTP"

    [Display(Name = "TOTP Secret Key")]
    [StringLength(100)]
    public string? TotpSecretKey { get; set; } // Base32 encoded secret for TOTP

    [Display(Name = "Email OTP")]
    [StringLength(10)]
    public string? EmailOtp { get; set; } // Current OTP code for email 2FA

    [Display(Name = "OTP Generated At")]
    public DateTime? OtpGeneratedAtUtc { get; set; } // When OTP was generated

    [Display(Name = "Failed 2FA Attempts")]
    public int Failed2FAAttempts { get; set; } = 0;

    // Navigation property for audit logs
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    // Navigation property for password history
    public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();

    // Navigation property for password reset tokens
    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
