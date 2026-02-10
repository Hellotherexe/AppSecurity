namespace BookwormsOnline.Configuration;

/// <summary>
/// Configuration settings for Google reCAPTCHA v3
/// </summary>
public class ReCaptchaSettings
{
    /// <summary>
    /// reCAPTCHA Site Key (public key used in client-side)
    /// </summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// reCAPTCHA Secret Key (private key used in server-side verification)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Minimum score required to pass verification (0.0 to 1.0)
    /// Default: 0.5 (recommended by Google)
    /// </summary>
    public double MinimumScore { get; set; } = 0.5;

    /// <summary>
    /// Google reCAPTCHA verification endpoint URL
    /// </summary>
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
}
