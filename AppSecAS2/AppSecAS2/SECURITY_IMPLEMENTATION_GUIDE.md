# Security Implementation Guide - reCAPTCHA v3 & Security Best Practices

## Overview
This document describes the complete implementation of Google reCAPTCHA v3 and security best practices including XSS prevention, SQL injection protection, CSRF protection, and input validation.

---

## ?? Google reCAPTCHA v3 Implementation

### Features
- ? **Invisible reCAPTCHA** - No user interaction required
- ? **Score-based verification** - Returns score from 0.0 (bot) to 1.0 (human)
- ? **Action-based verification** - Validates expected action
- ? **Configurable threshold** - Set minimum acceptable score
- ? **Integrated on Registration** - Protects member registration
- ? **Integrated on Login** - Protects login attempts

---

## Configuration

### 1. Get reCAPTCHA Keys

Visit: https://www.google.com/recaptcha/admin/create

1. **Register a new site**
2. Select **reCAPTCHA v3**
3. Add your **domains** (e.g., localhost, yourdomain.com)
4. Get your **Site Key** (public) and **Secret Key** (private)

### 2. Update appsettings.json

```json
{
  "ReCaptcha": {
    "SiteKey": "YOUR_SITE_KEY_HERE",
    "SecretKey": "YOUR_SECRET_KEY_HERE",
    "MinimumScore": 0.5,
    "VerifyUrl": "https://www.google.com/recaptcha/api/siteverify"
  }
}
```

**Configuration Options:**
- **SiteKey**: Public key for client-side (used in views)
- **SecretKey**: Private key for server-side verification
- **MinimumScore**: Threshold from 0.0 to 1.0 (default: 0.5)
  - `0.0-0.4`: Likely a bot
  - `0.5-0.6`: Suspicious
  - `0.7-1.0`: Likely human
- **VerifyUrl**: Google's verification endpoint

---

## Service Implementation

### ReCaptchaSettings.cs
```csharp
public class ReCaptchaSettings
{
    public string SiteKey { get; set; }
    public string SecretKey { get; set; }
    public double MinimumScore { get; set; } = 0.5;
    public string VerifyUrl { get; set; }
}
```

### RecaptchaService.cs

**Key Methods:**
```csharp
public async Task<RecaptchaVerificationResult> VerifyTokenAsync(
    string token, 
    string expectedAction, 
    string? remoteIp = null)
```

**Verification Process:**
1. Validate token is not empty
2. POST token to Google's API with secret key
3. Parse JSON response
4. Check `success` flag
5. Verify action matches expected action
6. Check score meets minimum threshold
7. Return verification result

**Response Model:**
```csharp
public class RecaptchaVerificationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double Score { get; set; }
    public string? Action { get; set; }
    public DateTime? ChallengeTimestamp { get; set; }
    public string? Hostname { get; set; }
}
```

---

## Client-Side Implementation

### Registration Form (Register.cshtml)

**1. Inject reCAPTCHA Settings:**
```razor
@inject Microsoft.Extensions.Options.IOptions<BookwormsOnline.Configuration.ReCaptchaSettings> ReCaptchaSettings

@{
    var siteKey = ReCaptchaSettings.Value.SiteKey;
}
```

**2. Add Hidden Token Field:**
```html
<form asp-action="Register" method="post" enctype="multipart/form-data" id="registrationForm">
    @Html.AntiForgeryToken()
    <input type="hidden" id="recaptchaToken" name="recaptchaToken" />
    <!-- other form fields -->
</form>
```

**3. Include reCAPTCHA Script:**
```html
<script src="https://www.google.com/recaptcha/api.js?render=@siteKey"></script>
```

**4. Execute reCAPTCHA on Submit:**
```javascript
form.addEventListener('submit', function(e) {
    e.preventDefault();
    
    // Execute reCAPTCHA v3
    grecaptcha.ready(function() {
        grecaptcha.execute('@siteKey', {action: 'register'}).then(function(token) {
            // Set token in hidden field
            document.getElementById('recaptchaToken').value = token;
            
            // Submit form
            form.submit();
        }).catch(function(error) {
            console.error('reCAPTCHA error:', error);
            alert('reCAPTCHA verification failed. Please refresh and try again.');
        });
    });
});
```

### Login Form (Login.cshtml)

Similar implementation with `action: 'login'`:

```javascript
grecaptcha.execute('@siteKey', {action: 'login'}).then(function(token) {
    document.getElementById('recaptchaToken').value = token;
    form.submit();
});
```

---

## Server-Side Verification

### MemberController.Register

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(
    Member model, 
    string Password, 
    string ConfirmPassword, 
    string? CreditCard, 
    IFormFile? Photo, 
    string? recaptchaToken)
{
    // Verify reCAPTCHA token
    if (string.IsNullOrEmpty(recaptchaToken))
    {
        ModelState.AddModelError("", "reCAPTCHA verification is required.");
        return View(model);
    }

    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var recaptchaResult = await _recaptchaService.VerifyTokenAsync(
        recaptchaToken, "register", clientIp);

    if (!recaptchaResult.Success)
    {
        _logger.LogWarning("reCAPTCHA verification failed: {Error}", 
            recaptchaResult.ErrorMessage);
        ModelState.AddModelError("", "Bot detection failed. Please try again.");
        return View(model);
    }

    _logger.LogInformation("reCAPTCHA verification successful. Score: {Score}", 
        recaptchaResult.Score);

    // Continue with registration logic...
}
```

### AccountController.Login

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
{
    // Verify reCAPTCHA token
    if (string.IsNullOrEmpty(model.RecaptchaToken))
    {
        ModelState.AddModelError("", "reCAPTCHA verification is required.");
        return View(model);
    }

    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    var recaptchaResult = await _recaptchaService.VerifyTokenAsync(
        model.RecaptchaToken, "login", clientIp);

    if (!recaptchaResult.Success)
    {
        _logger.LogWarning("reCAPTCHA verification failed: {Error}", 
            recaptchaResult.ErrorMessage);
        ModelState.AddModelError("", "Bot detection failed. Please try again.");
        await _auditLogService.LogAsync(null, "LoginFailedRecaptcha", 
            $"Email: {model.Email}");
        return View(model);
    }

    // Continue with login logic...
}
```

---

## ??? Security Best Practices

### 1. SQL Injection Prevention

**? Entity Framework Core**
- All database access uses EF Core
- Parameterized queries automatically
- No raw SQL strings

**Example:**
```csharp
// SAFE - Parameterized query
var member = await _context.Members
    .FirstOrDefaultAsync(m => m.Email.ToLower() == model.Email.ToLower());

// UNSAFE - Never do this!
// var sql = $"SELECT * FROM Members WHERE Email = '{email}'";
```

**Benefits:**
- ? Automatic parameter binding
- ? Type safety
- ? Protection against SQL injection
- ? LINQ query translation

---

### 2. Cross-Site Scripting (XSS) Prevention

**? Razor Automatic Encoding**
- All `@` expressions are HTML-encoded by default
- Prevents script injection

**Safe Examples:**
```razor
<!-- SAFE - Automatic encoding -->
<p>@Model.ShippingAddress</p>
<span>@Model.FirstName</span>

<!-- SAFE - Explicit encoding -->
<p>@Html.Encode(Model.ShippingAddress)</p>

<!-- UNSAFE - Never do this! -->
@* @Html.Raw(Model.ShippingAddress) *@
```

**Input Validation:**
```csharp
[RegularExpression(@"^[a-zA-Z0-9\s\.,#\-/()]+$", 
    ErrorMessage = "Shipping address contains invalid characters.")]
public string ShippingAddress { get; set; }
```

**Whitelist Approach:**
- Only allow specific characters
- Reject everything else
- Validate on server-side

---

### 3. Cross-Site Request Forgery (CSRF) Protection

**? Anti-Forgery Tokens**

**In Controllers:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]  // ? Required!
public async Task<IActionResult> Register(...)
```

**In Views:**
```razor
<form asp-action="Register" method="post">
    @Html.AntiForgeryToken()  <!-- ? Required! -->
    <!-- form fields -->
</form>
```

**How It Works:**
1. Server generates unique token per session
2. Token stored in cookie and form field
3. On submit, both tokens must match
4. Prevents forged requests from other sites

**All POST Actions Protected:**
- ? Register
- ? Login
- ? Logout
- ? All data modification actions

---

### 4. Input Validation

**DataAnnotations Validation:**
```csharp
[Required(ErrorMessage = "First name is required")]
[StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
[Display(Name = "First Name")]
public string FirstName { get; set; }

[Required(ErrorMessage = "Email is required")]
[EmailAddress(ErrorMessage = "Invalid email address format")]
[StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
public string Email { get; set; }

[Required(ErrorMessage = "Mobile number is required")]
[RegularExpression(@"^[89]\d{7}$", 
    ErrorMessage = "Mobile number must be a valid Singapore mobile number")]
public string MobileNo { get; set; }
```

**Server-Side Validation:**
```csharp
if (!ModelState.IsValid)
{
    return View(model);
}
```

**Client-Side Validation:**
```html
<span asp-validation-for="Email" class="text-danger"></span>
```

**Benefits:**
- ? Data integrity
- ? Type safety
- ? User-friendly errors
- ? Consistent validation

---

### 5. Safe Error Messages

**? Never Echo Raw User Input:**

**Safe:**
```csharp
// Generic error message
ModelState.AddModelError("", "Invalid login attempt.");

// Encoded in view
@Html.ValidationSummary(excludePropertyErrors: false, 
    message: "", 
    htmlAttributes: new { @class = "text-danger" })
```

**Unsafe:**
```csharp
// DON'T DO THIS!
ModelState.AddModelError("", $"User {userInput} not found.");
```

**Error Display:**
```razor
<!-- SAFE - Validation messages are automatically encoded -->
<div asp-validation-summary="All" class="alert alert-danger"></div>
<span asp-validation-for="Email" class="text-danger"></span>
```

---

## Security Checklist

### ? reCAPTCHA v3
- [x] Site Key and Secret Key configured
- [x] Minimum score set (0.5)
- [x] Integrated on Registration form
- [x] Integrated on Login form
- [x] Server-side verification implemented
- [x] Failed attempts logged

### ? SQL Injection Prevention
- [x] Entity Framework Core used for all queries
- [x] No raw SQL strings
- [x] Parameterized queries automatic
- [x] LINQ for database access

### ? XSS Prevention
- [x] Razor automatic encoding for all output
- [x] No Html.Raw() used
- [x] Input validation with RegularExpression
- [x] Whitelist approach for special characters
- [x] Server-side validation enforced

### ? CSRF Protection
- [x] [ValidateAntiForgeryToken] on all POST actions
- [x] @Html.AntiForgeryToken() in all forms
- [x] Anti-forgery tokens validated

### ? Input Validation
- [x] DataAnnotations on all models
- [x] Required fields validated
- [x] StringLength limits enforced
- [x] RegularExpression for format validation
- [x] EmailAddress validation
- [x] Server-side validation required
- [x] Client-side validation for UX

### ? Error Handling
- [x] Generic error messages (no info disclosure)
- [x] User input not echoed in errors
- [x] Validation messages HTML-encoded
- [x] Logging for debugging (not user-visible)

---

## Testing

### reCAPTCHA Testing

**Development Testing:**
1. Use test keys from Google (always pass)
2. **Test Site Key:** `6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI`
3. **Test Secret Key:** `6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe`

**Production Testing:**
1. Test with real keys
2. Verify score threshold works
3. Test action validation
4. Check logging

### Security Testing

**SQL Injection:**
```
Test input: ' OR '1'='1' --
Expected: Rejected by parameterized queries
```

**XSS:**
```
Test input: <script>alert('XSS')</script>
Expected: Encoded as &lt;script&gt;alert('XSS')&lt;/script&gt;
```

**CSRF:**
```
Test: Submit form without anti-forgery token
Expected: 400 Bad Request
```

---

## Troubleshooting

### reCAPTCHA Issues

**"Bot detection failed"**
- Check Site Key is correct
- Verify Secret Key is correct
- Check internet connection
- Verify domain is registered

**Score Too Low**
- Lower MinimumScore threshold
- Check for browser extensions blocking reCAPTCHA
- Verify site is not flagged as suspicious

**Action Mismatch**
- Verify action name matches in client and server
- Check for typos in action name

### Validation Issues

**ModelState Always Invalid**
- Check DataAnnotations match input
- Verify required fields are provided
- Check RegularExpression patterns

---

## Files Created/Modified

### New Files
1. ? `Configuration/ReCaptchaSettings.cs`
2. ? `Services/RecaptchaService.cs`

### Modified Files
1. ? `appsettings.json` - Added reCAPTCHA configuration
2. ? `Program.cs` - Registered RecaptchaService
3. ? `Models/Member.cs` - Added validation to ShippingAddress
4. ? `Models/LoginViewModel.cs` - Added RecaptchaToken property
5. ? `Controllers/MemberController.cs` - Added reCAPTCHA verification
6. ? `Controllers/AccountController.cs` - Added reCAPTCHA verification
7. ? `Views/Member/Register.cshtml` - Added reCAPTCHA script and token
8. ? `Views/Account/Login.cshtml` - Added reCAPTCHA script and token

---

## Summary

? **reCAPTCHA v3** integrated on Registration and Login
? **Score-based verification** with configurable threshold
? **SQL Injection** prevented with Entity Framework Core
? **XSS Prevention** with automatic Razor encoding
? **CSRF Protection** with anti-forgery tokens
? **Input Validation** with DataAnnotations
? **Secure Error Messages** without info disclosure
? **Comprehensive Logging** for security events

The application is now protected against common web vulnerabilities! ??
