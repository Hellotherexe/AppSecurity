using BookwormsOnline.Data;
using BookwormsOnline.Models;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using QRCoder;
using System.Security.Cryptography;

namespace BookwormsOnline.Services;

/// <summary>
/// Service for handling Two-Factor Authentication (2FA)
/// </summary>
public class TwoFactorAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<TwoFactorAuthService> _logger;

    // Configuration constants
    private const int EmailOtpLength = 6;
    private const int EmailOtpExpirationMinutes = 10;
    private const int Max2FAAttempts = 3;
    private const string TotpIssuer = "BookwormsOnline";

    public TwoFactorAuthService(
        ApplicationDbContext context,
        AuditLogService auditLogService,
        ILogger<TwoFactorAuthService> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a random 6-digit OTP for email 2FA
    /// </summary>
    public async Task<string> GenerateEmailOtpAsync(int memberId)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            throw new InvalidOperationException("Member not found");
        }

        // Generate random 6-digit code
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        // Store OTP and timestamp
        member.EmailOtp = otp;
        member.OtpGeneratedAtUtc = DateTime.UtcNow;
        member.Failed2FAAttempts = 0; // Reset failed attempts

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(memberId, "EmailOtpGenerated", 
            $"OTP generated, expires in {EmailOtpExpirationMinutes} minutes");

        _logger.LogInformation("Email OTP generated for MemberId: {MemberId}", memberId);

        return otp;
    }

    /// <summary>
    /// Verify email OTP code
    /// </summary>
    public async Task<TwoFactorVerificationResult> VerifyEmailOtpAsync(int memberId, string code)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "Member not found"
            };
        }

        // Check if OTP exists
        if (string.IsNullOrEmpty(member.EmailOtp))
        {
            await _auditLogService.LogAsync(memberId, "2FAVerificationFailed", "No OTP found");
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "No verification code found. Please request a new code."
            };
        }

        // Check if OTP expired
        if (member.OtpGeneratedAtUtc == null || 
            DateTime.UtcNow - member.OtpGeneratedAtUtc.Value > TimeSpan.FromMinutes(EmailOtpExpirationMinutes))
        {
            await _auditLogService.LogAsync(memberId, "2FAVerificationFailed", "OTP expired");
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "Verification code has expired. Please request a new code."
            };
        }

        // Verify code matches
        if (member.EmailOtp != code)
        {
            member.Failed2FAAttempts++;
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync(memberId, "2FAVerificationFailed", "Incorrect code");

            var attemptsRemaining = Max2FAAttempts - member.Failed2FAAttempts;
            if (attemptsRemaining <= 0)
            {
                // Clear OTP to prevent further attempts
                member.EmailOtp = null;
                member.OtpGeneratedAtUtc = null;
                await _context.SaveChangesAsync();

                return new TwoFactorVerificationResult
                {
                    Success = false,
                    ErrorMessage = "Too many failed attempts. Please request a new code."
                };
            }

            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = $"Incorrect verification code. {attemptsRemaining} attempt(s) remaining."
            };
        }

        // Success - clear OTP
        member.EmailOtp = null;
        member.OtpGeneratedAtUtc = null;
        member.Failed2FAAttempts = 0;
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(memberId, "2FAVerificationSuccess", "Email OTP verified");

        _logger.LogInformation("Email OTP verified successfully for MemberId: {MemberId}", memberId);

        return new TwoFactorVerificationResult
        {
            Success = true
        };
    }

    /// <summary>
    /// Generate TOTP secret key for authenticator app
    /// </summary>
    public string GenerateTotpSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160 bits
        var base32Secret = Base32Encoding.ToString(key);
        return base32Secret;
    }

    /// <summary>
    /// Generate QR code for TOTP setup
    /// </summary>
    public string GenerateTotpQrCode(string email, string secretKey)
    {
        // Format: otpauth://totp/Issuer:email?secret=secretkey&issuer=Issuer
        var totpUri = $"otpauth://totp/{Uri.EscapeDataString(TotpIssuer)}:{Uri.EscapeDataString(email)}?secret={secretKey}&issuer={Uri.EscapeDataString(TotpIssuer)}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return $"data:image/png;base64,{Convert.ToBase64String(qrCodeImage)}";
    }

    /// <summary>
    /// Verify TOTP code from authenticator app
    /// </summary>
    public async Task<TwoFactorVerificationResult> VerifyTotpAsync(int memberId, string code)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "Member not found"
            };
        }

        if (string.IsNullOrEmpty(member.TotpSecretKey))
        {
            await _auditLogService.LogAsync(memberId, "2FAVerificationFailed", "No TOTP secret found");
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "TOTP not configured"
            };
        }

        try
        {
            var secretBytes = Base32Encoding.ToBytes(member.TotpSecretKey);
            var totp = new Totp(secretBytes);

            // Verify with time window (±1 step = ±30 seconds)
            var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));

            if (!isValid)
            {
                member.Failed2FAAttempts++;
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(memberId, "2FAVerificationFailed", "Incorrect TOTP code");

                var attemptsRemaining = Max2FAAttempts - member.Failed2FAAttempts;
                if (attemptsRemaining <= 0)
                {
                    return new TwoFactorVerificationResult
                    {
                        Success = false,
                        ErrorMessage = "Too many failed attempts. Please try again later."
                    };
                }

                return new TwoFactorVerificationResult
                {
                    Success = false,
                    ErrorMessage = $"Incorrect verification code. {attemptsRemaining} attempt(s) remaining."
                };
            }

            // Success
            member.Failed2FAAttempts = 0;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(memberId, "2FAVerificationSuccess", "TOTP verified");

            _logger.LogInformation("TOTP verified successfully for MemberId: {MemberId}", memberId);

            return new TwoFactorVerificationResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying TOTP for MemberId: {MemberId}", memberId);
            return new TwoFactorVerificationResult
            {
                Success = false,
                ErrorMessage = "Error verifying code"
            };
        }
    }

    /// <summary>
    /// Enable 2FA for a member
    /// </summary>
    public async Task<bool> Enable2FAAsync(int memberId, string twoFactorType, string? totpSecret = null)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return false;
        }

        member.TwoFactorEnabled = true;
        member.TwoFactorType = twoFactorType;

        if (twoFactorType == "TOTP" && !string.IsNullOrEmpty(totpSecret))
        {
            member.TotpSecretKey = totpSecret;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(memberId, "2FAEnabled", $"2FA enabled with {twoFactorType}");

        _logger.LogInformation("2FA enabled for MemberId: {MemberId}, Type: {Type}", memberId, twoFactorType);

        return true;
    }

    /// <summary>
    /// Disable 2FA for a member
    /// </summary>
    public async Task<bool> Disable2FAAsync(int memberId)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return false;
        }

        member.TwoFactorEnabled = false;
        member.TwoFactorType = null;
        member.TotpSecretKey = null;
        member.EmailOtp = null;
        member.OtpGeneratedAtUtc = null;
        member.Failed2FAAttempts = 0;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(memberId, "2FADisabled");

        _logger.LogInformation("2FA disabled for MemberId: {MemberId}", memberId);

        return true;
    }

    /// <summary>
    /// Check if member has 2FA enabled
    /// </summary>
    public async Task<bool> Is2FAEnabledAsync(int memberId)
    {
        var member = await _context.Members.FindAsync(memberId);
        return member?.TwoFactorEnabled ?? false;
    }
}

/// <summary>
/// Result of 2FA verification
/// </summary>
public class TwoFactorVerificationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
