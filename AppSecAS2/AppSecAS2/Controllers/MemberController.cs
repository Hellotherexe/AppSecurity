using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BookwormsOnline.Data;
using BookwormsOnline.Models;
using BookwormsOnline.Services;

namespace BookwormsOnline.Controllers;

public class MemberController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordPolicyService _passwordService;
    private readonly EncryptionService _encryptionService;
    private readonly IWebHostEnvironment _environment;
    private readonly IPasswordHasher<Member> _passwordHasher;
    private readonly RecaptchaService _recaptchaService;
    private readonly ILogger<MemberController> _logger;
    private const long MaxPhotoSizeBytes = 2 * 1024 * 1024; // 2 MB

    public MemberController(
        ApplicationDbContext context, 
        PasswordPolicyService passwordService,
        EncryptionService encryptionService,
        IWebHostEnvironment environment,
        IPasswordHasher<Member> passwordHasher,
        RecaptchaService recaptchaService,
        ILogger<MemberController> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _encryptionService = encryptionService;
        _environment = environment;
        _passwordHasher = passwordHasher;
        _recaptchaService = recaptchaService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult PasswordDemo()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(Member model, string Password, string ConfirmPassword, string? CreditCard, IFormFile? Photo, string? recaptchaToken)
    {
        // 0. Verify reCAPTCHA v3 token (REQUIRED)
        if (string.IsNullOrEmpty(recaptchaToken))
        {
            ModelState.AddModelError("", "reCAPTCHA verification is required.");
            return View(model);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var recaptchaResult = await _recaptchaService.VerifyTokenAsync(recaptchaToken, "register", clientIp);

        if (!recaptchaResult.Success)
        {
            _logger.LogWarning("reCAPTCHA verification failed for registration: {Error}", recaptchaResult.ErrorMessage);
            ModelState.AddModelError("", "Bot detection failed. Please try again.");
            return View(model);
        }

        _logger.LogInformation("reCAPTCHA verification successful. Score: {Score}", recaptchaResult.Score);

        // 1. Validate the model (DataAnnotations are automatically validated)
        
        // 2. Server-side password validation using PasswordPolicyService
        if (string.IsNullOrEmpty(Password))
        {
            ModelState.AddModelError("Password", "Password is required.");
        }
        else
        {
            var passwordValidation = _passwordService.ValidatePassword(Password);
            if (!passwordValidation.IsValid)
            {
                foreach (var error in passwordValidation.ErrorMessages)
                {
                    ModelState.AddModelError("Password", error);
                }
            }
        }

        // 3. Confirm password match
        if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
        }

        // 4. Check if email already exists
        var existingMember = await _context.Members
            .FirstOrDefaultAsync(m => m.Email.ToLower() == model.Email.ToLower());
        
        if (existingMember != null)
        {
            ModelState.AddModelError("Email", "A member with this email address is already registered.");
            return View(model);
        }

        // 5. Validate and encrypt credit card
        if (string.IsNullOrEmpty(CreditCard))
        {
            ModelState.AddModelError("CreditCard", "Credit card number is required.");
        }
        else
        {
            // Normalize: remove any non-digit characters
            string cleanedCard = System.Text.RegularExpressions.Regex.Replace(CreditCard ?? string.Empty, "\\D", "");

            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanedCard, @"^\d{13,19}$"))
            {
                ModelState.AddModelError("CreditCard", "Credit card number must be 13-19 digits.");
            }
            else if (!IsLuhnValid(cleanedCard))
            {
                ModelState.AddModelError("CreditCard", "Credit card number is invalid.");
            }
            else
            {
                try
                {
                    // Encrypt the cleaned digits-only card number
                    var (encrypted, masked) = _encryptionService.EncryptCreditCard(cleanedCard);
                    model.CreditCardEncrypted = encrypted;

                    // Store masked version in TempData to show confirmation
                    TempData["MaskedCard"] = masked;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("CreditCard", $"Error processing credit card: {ex.Message}");
                }
            }
        }

        // 6. Validate uploaded photo: required, JPG only, size limit <= 2 MB
        if (Photo == null || Photo.Length == 0)
        {
            ModelState.AddModelError("Photo", "Profile photo is required.");
        }
        else
        {
            // Check file extension (JPG only)
            var extension = Path.GetExtension(Photo.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg")
            {
                ModelState.AddModelError("Photo", "Only JPG/JPEG files are allowed.");
            }

            // Check file size (2 MB max)
            if (Photo.Length > MaxPhotoSizeBytes)
            {
                ModelState.AddModelError("Photo", $"Photo size must not exceed 2 MB. Current size: {Photo.Length / 1024 / 1024:F2} MB.");
            }

            // Verify it's actually an image
            try
            {
                using var image = System.Drawing.Image.FromStream(Photo.OpenReadStream());
                // If we get here, it's a valid image
            }
            catch
            {
                ModelState.AddModelError("Photo", "The uploaded file is not a valid image.");
            }
        }

        // 7. If validation failed, return the view with errors
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 8. Save the photo to wwwroot/uploads/photos with unique filename
        string? uniqueFileName = null;
        if (Photo != null)
        {
            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "photos");
                Directory.CreateDirectory(uploadsFolder);
                
                // Create unique filename using GUID + original extension
                uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(Photo.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await Photo.CopyToAsync(fileStream);
                }
                
                model.PhotoFileName = uniqueFileName;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Photo", $"Error saving photo: {ex.Message}");
                return View(model);
            }
        }

        // 9. Hash the password using ASP.NET Core Identity's PasswordHasher
        try
        {
            model.PasswordHash = _passwordHasher.HashPassword(model, Password);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("Password", $"Error hashing password: {ex.Message}");
            
            // Clean up uploaded photo if password hashing fails
            if (!string.IsNullOrEmpty(uniqueFileName))
            {
                var filePath = Path.Combine(_environment.WebRootPath, "uploads", "photos", uniqueFileName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            
            return View(model);
        }

        // 10. Set creation timestamp
        model.CreatedAt = DateTime.UtcNow;

        // 11. Save the Member entity to the database
        try
        {
            _context.Members.Add(model);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error saving member: {ex.Message}");
            
            // Clean up uploaded photo if database save fails
            if (!string.IsNullOrEmpty(uniqueFileName))
            {
                var filePath = Path.Combine(_environment.WebRootPath, "uploads", "photos", uniqueFileName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            
            return View(model);
        }

        // 12. After successful registration, redirect to Login page
        TempData["SuccessMessage"] = $"Registration successful! Welcome, {model.FirstName} {model.LastName}! Please log in with your credentials.";
        TempData["RegisteredEmail"] = model.Email;
        
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var member = await _context.Members.FindAsync(id);
        
        if (member == null)
        {
            return NotFound();
        }

        // Decrypt credit card and create masked version for display
        if (!string.IsNullOrEmpty(member.CreditCardEncrypted))
        {
            try
            {
                string decryptedCard = _encryptionService.Decrypt(member.CreditCardEncrypted);
                
                // Create masked version (show only last 4 digits)
                if (decryptedCard.Length >= 4)
                {
                    ViewBag.MaskedCreditCard = $"**** **** **** {decryptedCard.Substring(decryptedCard.Length - 4)}";
                }
                else
                {
                    ViewBag.MaskedCreditCard = "**** **** **** ****";
                }
            }
            catch (Exception ex)
            {
                ViewBag.MaskedCreditCard = "Error decrypting card";
                ViewBag.DecryptionError = ex.Message;
            }
        }
        else
        {
            ViewBag.MaskedCreditCard = "No card on file";
        }

        return View(member);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var members = await _context.Members.ToListAsync();
        
        // Create a dictionary to store masked credit cards
        var maskedCards = new Dictionary<int, string>();
        
        foreach (var member in members)
        {
            if (!string.IsNullOrEmpty(member.CreditCardEncrypted))
            {
                try
                {
                    string decryptedCard = _encryptionService.Decrypt(member.CreditCardEncrypted);
                    
                    // Create masked version
                    if (decryptedCard.Length >= 4)
                    {
                        maskedCards[member.MemberId] = $"**** **** **** {decryptedCard.Substring(decryptedCard.Length - 4)}";
                    }
                    else
                    {
                        maskedCards[member.MemberId] = "**** **** **** ****";
                    }
                }
                catch
                {
                    maskedCards[member.MemberId] = "Error decrypting";
                }
            }
            else
            {
                maskedCards[member.MemberId] = "No card on file";
            }
        }
        
        ViewBag.MaskedCards = maskedCards;
        return View(members);
    }

    // Luhn algorithm for basic credit card checksum validation
    private static bool IsLuhnValid(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return false;

        int sum = 0;
        bool alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digits[i])) return false;
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return (sum % 10) == 0;
    }
}
