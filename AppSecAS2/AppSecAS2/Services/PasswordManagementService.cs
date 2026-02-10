using BookwormsOnline.Data;
using BookwormsOnline.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookwormsOnline.Services;

/// <summary>
/// Service for managing password changes, history, and reset tokens
/// </summary>
public class PasswordManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<Member> _passwordHasher;
    private readonly PasswordPolicyService _passwordPolicyService;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<PasswordManagementService> _logger;

    // Password age policies (in minutes)
    public const int MinPasswordAgeMinutes = 1; // Minimum 1 minute between changes
    public const int MaxPasswordAgeDays = 90; // Maximum 90 days before requiring change
    public const int PasswordHistoryCount = 2; // Cannot reuse last 2 passwords
    public const int ResetTokenExpirationHours = 24; // Reset tokens expire after 24 hours

    public PasswordManagementService(
        ApplicationDbContext context,
        IPasswordHasher<Member> passwordHasher,
        PasswordPolicyService passwordPolicyService,
        AuditLogService auditLogService,
        ILogger<PasswordManagementService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Change password for a member with all policy checks
    /// </summary>
    public async Task<PasswordChangeResult> ChangePasswordAsync(
        int memberId, 
        string currentPassword, 
        string newPassword)
    {
        var member = await _context.Members
            .Include(m => m.PasswordHistories.OrderByDescending(p => p.ChangedAtUtc).Take(PasswordHistoryCount))
            .FirstOrDefaultAsync(m => m.MemberId == memberId);

        if (member == null)
        {
            return new PasswordChangeResult
            {
                Success = false,
                ErrorMessage = "Member not found."
            };
        }

        // 1. Verify current password
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            member, member.PasswordHash, currentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            await _auditLogService.LogAsync(memberId, "PasswordChangeFailed", "Incorrect current password");
            return new PasswordChangeResult
            {
                Success = false,
                ErrorMessage = "Current password is incorrect."
            };
        }

        // 2. Validate new password meets policy requirements
        var policyValidation = _passwordPolicyService.ValidatePassword(newPassword);
        if (!policyValidation.IsValid)
        {
            return new PasswordChangeResult
            {
                Success = false,
                ErrorMessage = "New password does not meet policy requirements.",
                PolicyErrors = policyValidation.ErrorMessages
            };
        }

        // 3. Check minimum password age
        if (member.PasswordChangedAtUtc.HasValue)
        {
            var timeSinceLastChange = DateTime.UtcNow - member.PasswordChangedAtUtc.Value;
            if (timeSinceLastChange.TotalMinutes < MinPasswordAgeMinutes)
            {
                var remainingMinutes = MinPasswordAgeMinutes - timeSinceLastChange.TotalMinutes;
                return new PasswordChangeResult
                {
                    Success = false,
                    ErrorMessage = $"Password can only be changed once every {MinPasswordAgeMinutes} minute(s). " +
                                 $"Please wait {Math.Ceiling(remainingMinutes)} more minute(s)."
                };
            }
        }

        // 4. Check password history (cannot reuse last N passwords)
        var recentPasswords = await _context.PasswordHistories
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.ChangedAtUtc)
            .Take(PasswordHistoryCount)
            .ToListAsync();

        foreach (var historicalPassword in recentPasswords)
        {
            var historyVerification = _passwordHasher.VerifyHashedPassword(
                member, historicalPassword.PasswordHash, newPassword);

            if (historyVerification != PasswordVerificationResult.Failed)
            {
                return new PasswordChangeResult
                {
                    Success = false,
                    ErrorMessage = $"Password cannot be the same as your last {PasswordHistoryCount} passwords. " +
                                 "Please choose a different password."
                };
            }
        }

        // 5. All validations passed - update password
        var newPasswordHash = _passwordHasher.HashPassword(member, newPassword);
        
        // Save old password to history
        var passwordHistory = new PasswordHistory
        {
            MemberId = memberId,
            PasswordHash = member.PasswordHash,
            ChangedAtUtc = member.PasswordChangedAtUtc ?? member.CreatedAt
        };
        _context.PasswordHistories.Add(passwordHistory);

        // Update member password
        member.PasswordHash = newPasswordHash;
        member.PasswordChangedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log successful password change
        await _auditLogService.LogAsync(memberId, "PasswordChanged", "Password changed successfully");

        _logger.LogInformation("Password changed successfully for MemberId: {MemberId}", memberId);

        return new PasswordChangeResult
        {
            Success = true
        };
    }

    /// <summary>
    /// Generate a password reset token for forgot password flow
    /// </summary>
    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Email.ToLower() == email.ToLower());

        if (member == null)
        {
            // Don't reveal if email exists - return generic message
            var safeEmail = email.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogWarning("Password reset requested for non-existent email: {Email}", safeEmail);
            throw new InvalidOperationException("Email not found");
        }

        // Generate secure token
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        var resetToken = new PasswordResetToken
        {
            MemberId = member.MemberId,
            Token = token,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(ResetTokenExpirationHours),
            IsUsed = false
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(member.MemberId, "PasswordResetTokenGenerated", 
            $"Token expires at: {resetToken.ExpiresAtUtc}");

        _logger.LogInformation("Password reset token generated for MemberId: {MemberId}", member.MemberId);

        return token;
    }

    /// <summary>
    /// Validate and use a password reset token
    /// </summary>
    public async Task<PasswordResetResult> ResetPasswordWithTokenAsync(
        string token, 
        string newPassword)
    {
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.Member)
                .ThenInclude(m => m!.PasswordHistories.OrderByDescending(p => p.ChangedAtUtc).Take(PasswordHistoryCount))
            .FirstOrDefaultAsync(t => t.Token == token);

        if (resetToken == null)
        {
            var safeToken = token?.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogWarning("Invalid password reset token attempted: {Token}", safeToken);
            return new PasswordResetResult
            {
                Success = false,
                ErrorMessage = "Invalid or expired reset link."
            };
        }

        // Check if token is expired
        if (resetToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            await _auditLogService.LogAsync(resetToken.MemberId, "PasswordResetTokenExpired");
            return new PasswordResetResult
            {
                Success = false,
                ErrorMessage = "This reset link has expired. Please request a new one."
            };
        }

        // Check if token has already been used
        if (resetToken.IsUsed)
        {
            await _auditLogService.LogAsync(resetToken.MemberId, "PasswordResetTokenReused");
            return new PasswordResetResult
            {
                Success = false,
                ErrorMessage = "This reset link has already been used. Please request a new one."
            };
        }

        var member = resetToken.Member!;

        // Validate new password meets policy
        var policyValidation = _passwordPolicyService.ValidatePassword(newPassword);
        if (!policyValidation.IsValid)
        {
            return new PasswordResetResult
            {
                Success = false,
                ErrorMessage = "New password does not meet policy requirements.",
                PolicyErrors = policyValidation.ErrorMessages
            };
        }

        // Check password history
        var recentPasswords = member.PasswordHistories.ToList();
        foreach (var historicalPassword in recentPasswords)
        {
            var historyVerification = _passwordHasher.VerifyHashedPassword(
                member, historicalPassword.PasswordHash, newPassword);

            if (historyVerification != PasswordVerificationResult.Failed)
            {
                return new PasswordResetResult
                {
                    Success = false,
                    ErrorMessage = $"Password cannot be the same as your last {PasswordHistoryCount} passwords. " +
                                 "Please choose a different password."
                };
            }
        }

        // All validations passed - reset password
        var newPasswordHash = _passwordHasher.HashPassword(member, newPassword);

        // Save old password to history
        var passwordHistory = new PasswordHistory
        {
            MemberId = member.MemberId,
            PasswordHash = member.PasswordHash,
            ChangedAtUtc = member.PasswordChangedAtUtc ?? member.CreatedAt
        };
        _context.PasswordHistories.Add(passwordHistory);

        // Update member password
        member.PasswordHash = newPasswordHash;
        member.PasswordChangedAtUtc = DateTime.UtcNow;

        // Mark token as used
        resetToken.IsUsed = true;
        resetToken.UsedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log successful password reset
        await _auditLogService.LogAsync(member.MemberId, "PasswordReset", "Password reset successfully");

        _logger.LogInformation("Password reset successfully for MemberId: {MemberId}", member.MemberId);

        return new PasswordResetResult
        {
            Success = true,
            MemberId = member.MemberId,
            Email = member.Email
        };
    }

    /// <summary>
    /// Check if member's password has expired
    /// </summary>
    public async Task<bool> IsPasswordExpiredAsync(int memberId)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null) return false;

        if (!member.PasswordChangedAtUtc.HasValue)
        {
            // If never changed, check against creation date
            var daysSinceCreation = (DateTime.UtcNow - member.CreatedAt).TotalDays;
            return daysSinceCreation > MaxPasswordAgeDays;
        }

        var daysSinceChange = (DateTime.UtcNow - member.PasswordChangedAtUtc.Value).TotalDays;
        return daysSinceChange > MaxPasswordAgeDays;
    }
}

/// <summary>
/// Result of password change operation
/// </summary>
public class PasswordChangeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? PolicyErrors { get; set; }
}

/// <summary>
/// Result of password reset operation
/// </summary>
public class PasswordResetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? PolicyErrors { get; set; }
    public int? MemberId { get; set; }
    public string? Email { get; set; }
}
