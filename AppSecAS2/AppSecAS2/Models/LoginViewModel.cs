using System.ComponentModel.DataAnnotations;

namespace BookwormsOnline.Models;

/// <summary>
/// View model for member login
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember Me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>
    /// reCAPTCHA token (hidden field, populated by JavaScript)
    /// </summary>
    public string? RecaptchaToken { get; set; }
}
