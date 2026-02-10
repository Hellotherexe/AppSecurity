using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BookwormsOnline.Data;
using BookwormsOnline.Models;
using BookwormsOnline.Services;

namespace BookwormsOnline.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<Member> _passwordHasher;
    private readonly AuditLogService _auditLogService;
    private readonly RecaptchaService _recaptchaService;
    private readonly TwoFactorAuthService _twoFactorAuthService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountController> _logger;

    // Configuration constants
    private const int MaxFailedLoginAttempts = 3;
    private const int FailedLoginWindowMinutes = 15;
    private const int LockoutDurationMinutes = 5;

    public AccountController(
        ApplicationDbContext context,
        IPasswordHasher<Member> passwordHasher,
        AuditLogService auditLogService,
        RecaptchaService recaptchaService,
        TwoFactorAuthService twoFactorAuthService,
        IEmailService emailService,
        ILogger<AccountController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _recaptchaService = recaptchaService;
        _twoFactorAuthService = twoFactorAuthService;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? message = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        // Display message from multiple login detection or other sources
        if (!string.IsNullOrEmpty(message))
        {
            ViewBag.InfoMessage = message;
        }
        
        // Display success message if redirected from registration
        if (TempData["SuccessMessage"] != null)
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            ViewBag.RegisteredEmail = TempData["RegisteredEmail"];
        }
        
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 0. Verify reCAPTCHA v3 token (REQUIRED)
        if (string.IsNullOrEmpty(model.RecaptchaToken))
        {
            ModelState.AddModelError("", "reCAPTCHA verification is required.");
            return View(model);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var recaptchaResult = await _recaptchaService.VerifyTokenAsync(model.RecaptchaToken, "login", clientIp);

        if (!recaptchaResult.Success)
        {
            _logger.LogWarning("reCAPTCHA verification failed for login: {Error}", recaptchaResult.ErrorMessage);
            ModelState.AddModelError("", "Bot detection failed. Please try again.");

            // Log failed attempt with reCAPTCHA failure
            await _auditLogService.LogAsync(null, "LoginFailedRecaptcha", $"Email: {model.Email}");

            return View(model);
        }

        _logger.LogInformation("reCAPTCHA verification successful for login. Score: {Score}", recaptchaResult.Score);

        // 1. Find member by email (case-insensitive)
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Email.ToLower() == model.Email.ToLower());

        // 2. Check if member exists
        if (member == null)
        {
            // Generic error message to prevent email enumeration
            ModelState.AddModelError("", "Invalid login attempt.");
            
            // Log failed attempt with email (for admin purposes)
            await _auditLogService.LogAsync(null, "LoginFailed", $"Email not found: {model.Email}");
            
            return View(model);
        }

        // 3. Check if account is locked out
        if (member.LockoutEndUtc.HasValue && member.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remainingLockout = member.LockoutEndUtc.Value - DateTime.UtcNow;
            ModelState.AddModelError("", 
                $"Account is locked due to multiple failed login attempts. " +
                $"Please try again in {Math.Ceiling(remainingLockout.TotalMinutes)} minute(s).");
            
            await _auditLogService.LogAsync(member.MemberId, "LoginAttemptWhileLocked");
            
            return View(model);
        }

        // 4. Verify password
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            member, member.PasswordHash, model.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            // Password is incorrect - increment failed login count
            member.FailedLoginCount++;
            
            // Check if we should lock the account
            var recentFailedLogins = await GetRecentFailedLoginsAsync(member.MemberId);
            
            if (recentFailedLogins >= MaxFailedLoginAttempts - 1) // -1 because we're about to add one more
            {
                // Lock the account
                member.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                _logger.LogWarning(
                    "Account locked for MemberId: {MemberId} due to {Count} failed login attempts",
                    member.MemberId, MaxFailedLoginAttempts);
                
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(member.MemberId, "AccountLocked", 
                    $"Locked for {LockoutDurationMinutes} minutes after {MaxFailedLoginAttempts} failed attempts");
                
                ModelState.AddModelError("", 
                    $"Account locked due to {MaxFailedLoginAttempts} failed login attempts. " +
                    $"Please try again after {LockoutDurationMinutes} minutes.");
            }
            else
            {
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(member.MemberId, "LoginFailed", "Incorrect password");
                
                var attemptsRemaining = MaxFailedLoginAttempts - recentFailedLogins - 1;
                ModelState.AddModelError("", 
                    $"Invalid login attempt. {attemptsRemaining} attempt(s) remaining before account lockout.");
            }
            
            return View(model);
        }

        // 5. Password is correct - Reset failed login attempts
        member.FailedLoginCount = 0;
        member.LockoutEndUtc = null;
        member.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 6. Check if 2FA is enabled
        if (member.TwoFactorEnabled)
        {
            // Store member ID in session for 2FA verification
            HttpContext.Session.SetInt32("2FA_MemberId", member.MemberId);
            HttpContext.Session.SetString("2FA_Email", member.Email);
            HttpContext.Session.SetString("2FA_Type", member.TwoFactorType ?? "Email");
            HttpContext.Session.SetString("2FA_ReturnUrl", returnUrl ?? "/");
            HttpContext.Session.SetString("2FA_RememberMe", model.RememberMe.ToString());

            // Generate and send OTP if Email 2FA
            if (member.TwoFactorType == "Email")
            {
                var otp = await _twoFactorAuthService.GenerateEmailOtpAsync(member.MemberId);
                await _emailService.Send2FACodeAsync(member.Email, otp);
                
                _logger.LogInformation("2FA Email OTP sent to MemberId: {MemberId}", member.MemberId);
            }

            await _auditLogService.LogAsync(member.MemberId, "2FARequired", 
                $"2FA verification required ({member.TwoFactorType})");

            // Redirect to 2FA verification page
            return RedirectToAction("Verify2FA");
        }

        // 7. No 2FA - Complete login process
        await CompleteLoginAsync(member, model.RememberMe);

        // 8. Log successful login
        await _auditLogService.LogAsync(member.MemberId, "LoginSuccess");
        
        _logger.LogInformation("Successful login for MemberId: {MemberId}", member.MemberId);

        // 9. Redirect to return URL or default page
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Details", "Member", new { id = member.MemberId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Get member ID before signing out
        var memberIdClaim = User.FindFirst("MemberId")?.Value;
        
        if (int.TryParse(memberIdClaim, out int memberId))
        {
            // Log logout
            await _auditLogService.LogAsync(memberId, "Logout");
            
            // Clear session ID from database
            var member = await _context.Members.FindAsync(memberId);
            if (member != null)
            {
                member.CurrentSessionId = null;
                await _context.SaveChangesAsync();
            }
        }

        // Sign out (clear authentication cookie)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Clear session
        HttpContext.Session.Clear();

        TempData["SuccessMessage"] = "You have been logged out successfully.";
        
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Get recent failed login attempts within the specified window
    /// </summary>
    private async Task<int> GetRecentFailedLoginsAsync(int memberId)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-FailedLoginWindowMinutes);
        
        return await _context.AuditLogs
            .Where(a => a.MemberId == memberId 
                     && a.Action == "LoginFailed" 
                     && a.TimestampUtc >= cutoffTime)
            .CountAsync();
    }

    /// <summary>
    /// Complete login process (generate session, sign in)
    /// </summary>
    private async Task CompleteLoginAsync(Member member, bool rememberMe)
    {
        // Generate new session ID
        var sessionId = Guid.NewGuid().ToString();
        member.CurrentSessionId = sessionId;
        
        await _context.SaveChangesAsync();

        // Store session ID in HttpContext.Session
        HttpContext.Session.SetString("SessionId", sessionId);

        // Create authentication claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, member.Email),
            new Claim("MemberId", member.MemberId.ToString()),
            new Claim(ClaimTypes.Name, $"{member.FirstName} {member.LastName}"),
            new Claim("FirstName", member.FirstName),
            new Claim("LastName", member.LastName)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(2),
            AllowRefresh = true
        };

        // Sign in the user
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);
    }

    /// <summary>
    /// GET: 2FA Verification Page
    /// </summary>
    [HttpGet]
    public IActionResult Verify2FA()
    {
        var memberId = HttpContext.Session.GetInt32("2FA_MemberId");
        if (!memberId.HasValue)
        {
            return RedirectToAction("Login");
        }

        var twoFactorType = HttpContext.Session.GetString("2FA_Type") ?? "Email";
        var email = HttpContext.Session.GetString("2FA_Email");
        var returnUrl = HttpContext.Session.GetString("2FA_ReturnUrl");

        ViewBag.Email = email;
        ViewBag.TwoFactorType = twoFactorType;

        return View(new TwoFactorVerificationViewModel
        {
            TwoFactorType = twoFactorType,
            ReturnUrl = returnUrl
        });
    }

    /// <summary>
    /// POST: Verify 2FA Code
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify2FA(TwoFactorVerificationViewModel model)
    {
        var memberId = HttpContext.Session.GetInt32("2FA_MemberId");
        if (!memberId.HasValue)
        {
            ModelState.AddModelError("", "Session expired. Please log in again.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Verify reCAPTCHA
        if (string.IsNullOrEmpty(model.RecaptchaToken))
        {
            ModelState.AddModelError("", "reCAPTCHA verification is required.");
            return View(model);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var recaptchaResult = await _recaptchaService.VerifyTokenAsync(model.RecaptchaToken, "verify2fa", clientIp);

        if (!recaptchaResult.Success)
        {
            _logger.LogWarning("reCAPTCHA verification failed for 2FA");
            ModelState.AddModelError("", "Bot detection failed. Please try again.");
            return View(model);
        }

        // Get member
        var member = await _context.Members.FindAsync(memberId.Value);
        if (member == null)
        {
            ModelState.AddModelError("", "Member not found.");
            return View(model);
        }

        // Verify 2FA code based on type
        TwoFactorVerificationResult verificationResult;

        if (model.TwoFactorType == "TOTP")
        {
            verificationResult = await _twoFactorAuthService.VerifyTotpAsync(memberId.Value, model.Code);
        }
        else // Email
        {
            verificationResult = await _twoFactorAuthService.VerifyEmailOtpAsync(memberId.Value, model.Code);
        }

        if (!verificationResult.Success)
        {
            ModelState.AddModelError("", verificationResult.ErrorMessage);
            return View(model);
        }

        // 2FA verification successful - complete login
        var rememberMe = HttpContext.Session.GetString("2FA_RememberMe") == "true";
        await CompleteLoginAsync(member, rememberMe);

        // Clear 2FA session data
        HttpContext.Session.Remove("2FA_MemberId");
        HttpContext.Session.Remove("2FA_Email");
        HttpContext.Session.Remove("2FA_Type");
        HttpContext.Session.Remove("2FA_RememberMe");

        var returnUrl = HttpContext.Session.GetString("2FA_ReturnUrl");
        HttpContext.Session.Remove("2FA_ReturnUrl");

        await _auditLogService.LogAsync(member.MemberId, "LoginSuccess", "Login completed after 2FA verification");

        _logger.LogInformation("2FA verification successful, login completed for MemberId: {MemberId}", member.MemberId);

        // Redirect
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Details", "Member", new { id = member.MemberId });
    }

    /// <summary>
    /// POST: Resend 2FA Code (for Email OTP)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend2FACode()
    {
        var memberId = HttpContext.Session.GetInt32("2FA_MemberId");
        if (!memberId.HasValue)
        {
            return Json(new { success = false, message = "Session expired" });
        }

        var member = await _context.Members.FindAsync(memberId.Value);
        if (member == null)
        {
            return Json(new { success = false, message = "Member not found" });
        }

        if (member.TwoFactorType != "Email")
        {
            return Json(new { success = false, message = "Code resend only available for Email 2FA" });
        }

        // Generate new OTP
        var otp = await _twoFactorAuthService.GenerateEmailOtpAsync(memberId.Value);
        await _emailService.Send2FACodeAsync(member.Email, otp);

        _logger.LogInformation("2FA code resent to MemberId: {MemberId}", memberId.Value);

        return Json(new { success = true, message = "Verification code sent to your email" });
    }

    /// <summary>
    /// GET: Forgot Password Page
    /// </summary>
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    /// <summary>
    /// POST: Send Password Reset Email
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email, string? recaptchaToken)
    {
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError("", "Email is required.");
            return View();
        }

        // Verify reCAPTCHA (optional for testing)
        if (!string.IsNullOrEmpty(recaptchaToken))
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var recaptchaResult = await _recaptchaService.VerifyTokenAsync(recaptchaToken, "forgotpassword", clientIp);

            if (!recaptchaResult.Success)
            {
                _logger.LogWarning("reCAPTCHA verification failed for forgot password");
                // Don't block - just log the warning for testing
            }
        }

        try
        {
            // Note: PasswordManagementService needs to be injected
            var passwordManagementService = HttpContext.RequestServices.GetRequiredService<PasswordManagementService>();
            var token = await passwordManagementService.GeneratePasswordResetTokenAsync(email);

            // Build reset URL
            var resetUrl = Url.Action(
                "ResetPassword",
                "Account",
                new { token = token },
                protocol: Request.Scheme);

            // Send email
            await _emailService.SendPasswordResetEmailAsync(email, resetUrl!);

            _logger.LogInformation("Password reset email sent to: {Email}", email);
            _logger.LogWarning("PASSWORD RESET URL (TESTING ONLY): {ResetUrl}", resetUrl);

            TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
            
            // TESTING ONLY - Show link on confirmation page
            TempData["ResetUrl"] = resetUrl;
            TempData["Email"] = email;
            
            return RedirectToAction("ForgotPasswordConfirmation");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Password reset failed for email: {Email}. Error: {Error}", email, ex.Message);
            // Don't reveal if email exists - show success anyway
            TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToAction("ForgotPasswordConfirmation");
        }
    }

    /// <summary>
    /// GET: Forgot Password Confirmation
    /// </summary>
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    /// <summary>
    /// GET: Reset Password Page
    /// </summary>
    [HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToAction("ForgotPassword");
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    /// <summary>
    /// POST: Reset Password with Token
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        try
        {
            var passwordManagementService = HttpContext.RequestServices.GetRequiredService<PasswordManagementService>();
            var result = await passwordManagementService.ResetPasswordWithTokenAsync(model.Token, model.NewPassword);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Password reset failed.");
                if (result.PolicyErrors != null)
                {
                    foreach (var error in result.PolicyErrors)
                    {
                        ModelState.AddModelError("NewPassword", error);
                    }
                }
                return View(model);
            }

            _logger.LogInformation("Password reset successful for MemberId: {MemberId}", result.MemberId);

            TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in with your new password.";
            TempData["RegisteredEmail"] = result.Email;
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password with token: {Token}", model.Token?.Replace("\r", "").Replace("\n", ""));
            ModelState.AddModelError("", "An error occurred while resetting your password. Please try again.");
            return View(model);
        }
    }

    /// <summary>
    /// GET: Change Password Page (for authenticated users)
    /// </summary>
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        return View();
    }

    /// <summary>
    /// POST: Change Password
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        var memberIdClaim = User.FindFirst("MemberId")?.Value;
        if (!int.TryParse(memberIdClaim, out int memberId))
        {
            ModelState.AddModelError("", "User session invalid.");
            return View(model);
        }

        try
        {
            var passwordManagementService = HttpContext.RequestServices.GetRequiredService<PasswordManagementService>();
            var result = await passwordManagementService.ChangePasswordAsync(
                memberId,
                model.CurrentPassword,
                model.NewPassword);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Password change failed.");
                if (result.PolicyErrors != null)
                {
                    foreach (var error in result.PolicyErrors)
                    {
                        ModelState.AddModelError("NewPassword", error);
                    }
                }
                return View(model);
            }

            _logger.LogInformation("Password changed successfully for MemberId: {MemberId}", memberId);

            TempData["SuccessMessage"] = "Your password has been changed successfully.";
            return RedirectToAction("Details", "Member", new { id = memberId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for MemberId: {MemberId}", memberId);
            ModelState.AddModelError("", "An error occurred while changing your password. Please try again.");
            return View(model);
        }
    }

    /// <summary>
    /// GET: Access Denied Page
    /// </summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// GET: 2FA Settings Page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TwoFactorSettings()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        var memberIdClaim = User.FindFirst("MemberId")?.Value;
        if (!int.TryParse(memberIdClaim, out int memberId))
        {
            return RedirectToAction("Login");
        }

        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return NotFound();
        }

        var viewModel = new TwoFactorSettingsViewModel
        {
            TwoFactorEnabled = member.TwoFactorEnabled,
            TwoFactorType = member.TwoFactorType ?? "Email",
            TotpSecretKey = member.TotpSecretKey
        };

        return View(viewModel);
    }

    /// <summary>
    /// POST: Enable/Disable 2FA
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableTwoFactor(string twoFactorType)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        var memberIdClaim = User.FindFirst("MemberId")?.Value;
        if (!int.TryParse(memberIdClaim, out int memberId))
        {
            return RedirectToAction("Login");
        }

        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return NotFound();
        }

        member.TwoFactorEnabled = true;
        member.TwoFactorType = twoFactorType;

        // Generate TOTP secret if needed
        if (twoFactorType == "TOTP" && string.IsNullOrEmpty(member.TotpSecretKey))
        {
            var totpSecret = _twoFactorAuthService.GenerateTotpSecret();
            member.TotpSecretKey = totpSecret;
        }

        await _context.SaveChangesAsync();

        // Sanitize user-provided twoFactorType before logging to prevent log forging
        var safeTwoFactorType = (twoFactorType ?? string.Empty)
            .Replace(Environment.NewLine, string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        await _auditLogService.LogAsync(memberId, "2FAEnabled", $"Type: {safeTwoFactorType}");

        _logger.LogInformation("2FA enabled for MemberId: {MemberId}, Type: {Type}", memberId, safeTwoFactorType);

        TempData["SuccessMessage"] = $"Two-Factor Authentication ({twoFactorType}) has been enabled successfully.";
        return RedirectToAction("TwoFactorSettings");
    }

    /// <summary>
    /// POST: Disable 2FA
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTwoFactor()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        var memberIdClaim = User.FindFirst("MemberId")?.Value;
        if (!int.TryParse(memberIdClaim, out int memberId))
        {
            return RedirectToAction("Login");
        }

        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
        {
            return NotFound();
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

        TempData["SuccessMessage"] = "Two-Factor Authentication has been disabled.";
        return RedirectToAction("TwoFactorSettings");
    }
}
