using System.ComponentModel.DataAnnotations;

namespace BookwormsOnline.Models;

/// <summary>
/// View model for 2FA verification
/// </summary>
public class TwoFactorVerificationViewModel
{
    [Required(ErrorMessage = "Verification code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    [Display(Name = "Verification Code")]
    public string Code { get; set; } = string.Empty;

    public string? TwoFactorType { get; set; } // "Email" or "TOTP"
    
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// reCAPTCHA token
    /// </summary>
    public string? RecaptchaToken { get; set; }
}

/// <summary>
/// View model for enabling 2FA
/// </summary>
public class Enable2FAViewModel
{
    [Required(ErrorMessage = "Please select a 2FA method")]
    [Display(Name = "2FA Method")]
    public string TwoFactorType { get; set; } = string.Empty; // "Email" or "TOTP"

    [Display(Name = "Email Address")]
    public string? Email { get; set; }

    [Display(Name = "Secret Key")]
    public string? SecretKey { get; set; }

    [Display(Name = "QR Code")]
    public string? QrCodeDataUrl { get; set; }

    [Required(ErrorMessage = "Verification code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    [Display(Name = "Verification Code")]
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>
/// View model for managing 2FA settings
/// </summary>
public class Manage2FAViewModel
{
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorType { get; set; }
    public string? Email { get; set; }
    public bool CanEnable2FA { get; set; } = true;
}

/// <summary>
/// View model for 2FA settings page
/// </summary>
public class TwoFactorSettingsViewModel
{
    public bool TwoFactorEnabled { get; set; }
    public string TwoFactorType { get; set; } = "Email";
    public string? TotpSecretKey { get; set; }
}

