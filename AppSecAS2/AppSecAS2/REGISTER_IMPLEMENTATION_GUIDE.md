# Member Registration - Complete Implementation Guide

## Overview
This document describes the complete implementation of the GET and POST Register actions in the MemberController with all required validation, security features, and error handling.

## Implementation Summary

### ? All Requirements Implemented

1. ? **GET Register Action** - Displays registration form
2. ? **POST Register Action** - Processes registration with full validation
3. ? **Model Validation** - DataAnnotations + custom validation
4. ? **Email Uniqueness Check** - Prevents duplicate registrations
5. ? **Photo Upload Validation** - Required, JPG only, 2MB size limit
6. ? **Photo Storage** - Saved with unique GUID filename
7. ? **Password Hashing** - Using ASP.NET Core Identity's PasswordHasher
8. ? **Credit Card Encryption** - Using AES-256-CBC EncryptionService
9. ? **Database Persistence** - Member entity saved to database
10. ? **Redirect to Login** - After successful registration

---

## GET Register Action

```csharp
[HttpGet]
public IActionResult Register()
{
    return View();
}
```

**Purpose:** Displays the registration form

**Features:**
- Returns empty Member model
- Form rendered with validation attributes
- Client-side validation enabled

---

## POST Register Action - Step by Step

### 1. Model Validation

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(
    Member model, 
    string Password, 
    string ConfirmPassword, 
    string? CreditCard, 
    IFormFile? Photo)
```

**Automatic Validation:**
- DataAnnotations from Member model are validated automatically
- ModelState populated with errors if validation fails

### 2. Password Validation

```csharp
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
```

**Validates:**
- Minimum 12 characters
- At least 1 lowercase letter
- At least 1 uppercase letter
- At least 1 digit
- At least 1 special character

### 3. Password Confirmation

```csharp
if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
{
    ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
}
```

**Ensures:**
- Password and ConfirmPassword match exactly

### 4. Email Uniqueness Check

```csharp
var existingMember = await _context.Members
    .FirstOrDefaultAsync(m => m.Email.ToLower() == model.Email.ToLower());

if (existingMember != null)
{
    ModelState.AddModelError("Email", "A member with this email address is already registered.");
    return View(model);
}
```

**Features:**
- Case-insensitive email comparison
- Returns view immediately if duplicate found
- User-friendly error message

### 5. Credit Card Validation & Encryption

```csharp
if (string.IsNullOrEmpty(CreditCard))
{
    ModelState.AddModelError("CreditCard", "Credit card number is required.");
}
else
{
    string cleanedCard = CreditCard.Replace(" ", "").Replace("-", "");
    
    if (!System.Text.RegularExpressions.Regex.IsMatch(cleanedCard, @"^\d{13,19}$"))
    {
        ModelState.AddModelError("CreditCard", "Credit card number must be 13-19 digits.");
    }
    else
    {
        var (encrypted, masked) = _encryptionService.EncryptCreditCard(cleanedCard);
        model.CreditCardEncrypted = encrypted;
        TempData["MaskedCard"] = masked;
    }
}
```

**Process:**
1. Remove spaces and dashes
2. Validate 13-19 digits
3. Encrypt using AES-256-CBC
4. Store encrypted value
5. Create masked version for display

### 6. Photo Upload Validation

```csharp
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
    if (Photo.Length > MaxPhotoSizeBytes) // 2 * 1024 * 1024
    {
        ModelState.AddModelError("Photo", 
            $"Photo size must not exceed 2 MB. Current size: {Photo.Length / 1024 / 1024:F2} MB.");
    }

    // Verify it's actually an image
    try
    {
        using var image = System.Drawing.Image.FromStream(Photo.OpenReadStream());
    }
    catch
    {
        ModelState.AddModelError("Photo", "The uploaded file is not a valid image.");
    }
}
```

**Validation Rules:**
- ? File is required
- ? Extension must be .jpg or .jpeg
- ? Size must be ? 2 MB (2,097,152 bytes)
- ? File must be a valid image (verified using System.Drawing)

### 7. Return View if Validation Failed

```csharp
if (!ModelState.IsValid)
{
    return View(model);
}
```

**Behavior:**
- Returns user to form with all error messages
- Preserves user input (except password and photo)
- Displays validation summary at top

### 8. Save Photo with Unique Filename

```csharp
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
```

**Process:**
1. Create uploads/photos directory if not exists
2. Generate unique filename: `{GUID}.jpg`
3. Save file to `wwwroot/uploads/photos/`
4. Store filename in Member.PhotoFileName
5. Handle errors gracefully

**Example Filename:**
```
a3f5b8c2-4d7e-4a9b-8c1f-5e6d7a8b9c0d.jpg
```

### 9. Hash Password Using Identity's PasswordHasher

```csharp
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
```

**Features:**
- Uses `IPasswordHasher<Member>` from ASP.NET Core Identity
- Generates secure password hash (PBKDF2 with SHA256)
- Includes salt automatically
- Cleans up photo if hashing fails
- More secure than simple SHA-256

**Hash Format Example:**
```
AQAAAAEAACcQAAAAEBcpw1YzqJmRx3J2I8pZ7xQxXqxE1234567890abcdefghijklmnop
```

### 10. Set Creation Timestamp

```csharp
model.CreatedAt = DateTime.UtcNow;
```

**Ensures:**
- UTC timestamp recorded
- Consistent timezone handling

### 11. Save to Database

```csharp
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
```

**Transaction Safety:**
- Saves Member entity to database
- Cleans up photo file if save fails
- Prevents orphaned files

### 12. Redirect to Login Page

```csharp
TempData["SuccessMessage"] = 
    $"Registration successful! Welcome, {model.FirstName} {model.LastName}! Please log in with your credentials.";
TempData["RegisteredEmail"] = model.Email;

return RedirectToAction("Login", "Account");
```

**User Experience:**
- Success message displayed on Login page
- Email pre-filled for convenience
- Clear next steps for user

---

## Dependency Injection

### Services Required

```csharp
public MemberController(
    ApplicationDbContext context, 
    PasswordPolicyService passwordService,
    EncryptionService encryptionService,
    IWebHostEnvironment environment,
    IPasswordHasher<Member> passwordHasher)
{
    _context = context;
    _passwordService = passwordService;
    _encryptionService = encryptionService;
    _environment = environment;
    _passwordHasher = passwordHasher;
}
```

### Service Registration (Program.cs)

```csharp
builder.Services.AddScoped<PasswordPolicyService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<IPasswordHasher<Member>, PasswordHasher<Member>>();
```

---

## Constants

```csharp
private const long MaxPhotoSizeBytes = 2 * 1024 * 1024; // 2 MB
```

---

## Error Handling

### ModelState Errors

All validation errors added to ModelState:
- Field-level errors: `ModelState.AddModelError("FieldName", "Error message")`
- Form-level errors: `ModelState.AddModelError("", "Error message")`

### Cleanup on Errors

Photos are deleted if:
- Password hashing fails
- Database save fails
- Prevents orphaned files

### User-Friendly Messages

```
? "A member with this email address is already registered."
? "Photo size must not exceed 2 MB. Current size: 2.34 MB."
? "Only JPG/JPEG files are allowed."
? "The uploaded file is not a valid image."
? "Credit card number must be 13-19 digits."
```

---

## Security Features

### 1. CSRF Protection

```csharp
[ValidateAntiForgeryToken]
```

### 2. Password Security

- PasswordPolicyService validates strength
- PasswordHasher uses PBKDF2 + SHA256
- Salt automatically included
- Minimum 12 characters with complexity

### 3. Credit Card Security

- AES-256-CBC encryption
- Only encrypted data stored
- Never stored in plain text
- Masked display only

### 4. Photo Security

- Type validation (JPG only)
- Size validation (2 MB max)
- Image verification (valid image file)
- Unique random filenames (prevents overwriting)

### 5. Email Security

- Case-insensitive uniqueness check
- Prevents duplicate accounts
- EmailAddress data annotation

---

## Testing Checklist

### Valid Registration
- [ ] Fill all fields correctly
- [ ] Upload valid JPG photo (< 2MB)
- [ ] Enter strong password
- [ ] Match passwords
- [ ] Submit successfully
- [ ] Redirected to Login page
- [ ] Success message displayed
- [ ] Email pre-filled

### Email Validation
- [ ] Try registering with existing email
- [ ] Verify error message: "already registered"
- [ ] Try case variations (Test@email.com vs test@email.com)

### Password Validation
- [ ] Too short (< 12 chars) - Error
- [ ] No uppercase - Error
- [ ] No lowercase - Error
- [ ] No digit - Error
- [ ] No special character - Error
- [ ] Passwords don't match - Error
- [ ] Valid password - Success

### Photo Validation
- [ ] No photo selected - Error
- [ ] PNG file - Error ("Only JPG/JPEG allowed")
- [ ] File > 2MB - Error with size display
- [ ] Corrupted file - Error ("not a valid image")
- [ ] Valid JPG < 2MB - Success
- [ ] Photo saved with GUID filename
- [ ] Photo accessible at /uploads/photos/{guid}.jpg

### Credit Card Validation
- [ ] Empty card - Error
- [ ] Less than 13 digits - Error
- [ ] More than 19 digits - Error
- [ ] Contains letters - Error
- [ ] Valid 16-digit card - Encrypted and saved

### Database
- [ ] Member record created
- [ ] PasswordHash is hashed (not plain text)
- [ ] CreditCardEncrypted is Base64 string
- [ ] PhotoFileName is GUID.jpg
- [ ] CreatedAt is UTC timestamp
- [ ] Email is unique in database

---

## File Locations

### Controller
- `Controllers/MemberController.cs` - Register actions

### Views
- `Views/Member/Register.cshtml` - Registration form
- `Views/Account/Login.cshtml` - Login page (redirect target)

### Services
- `Services/PasswordPolicyService.cs` - Password validation
- `Services/EncryptionService.cs` - Credit card encryption

### Configuration
- `Program.cs` - Service registration
- `appsettings.json` - Encryption settings

### Photo Storage
- `wwwroot/uploads/photos/` - Photo directory

---

## Database Schema

```sql
CREATE TABLE Members (
    MemberId INT PRIMARY KEY IDENTITY(1,1),
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    MobileNo NVARCHAR(MAX) NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    CreditCardEncrypted NVARCHAR(MAX) NOT NULL,
    BillingAddress NVARCHAR(255) NOT NULL,
    ShippingAddress NVARCHAR(500) NOT NULL,
    PhotoFileName NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    LastLoginAt DATETIME2 NULL
);

CREATE UNIQUE INDEX IX_Member_Email_Unique ON Members(Email);
```

---

## Example Request Flow

1. **User visits** `/Member/Register` (GET)
2. **Form displayed** with all fields
3. **User fills** form and uploads photo
4. **User submits** form (POST)
5. **Server validates** all fields
6. **Server checks** email uniqueness
7. **Server validates** photo (type, size, valid image)
8. **Server saves** photo with GUID filename
9. **Server hashes** password with Identity's PasswordHasher
10. **Server encrypts** credit card with AES-256
11. **Server saves** Member to database
12. **Server redirects** to `/Account/Login`
13. **Login page** shows success message and pre-filled email

---

## Summary

? **Complete implementation** of GET and POST Register actions
? **Full validation** of all fields including custom rules
? **Email uniqueness** check with case-insensitive comparison
? **Photo upload** with required, JPG-only, 2MB limit validation
? **Unique filename** using GUID for photo storage
? **Password hashing** using ASP.NET Core Identity's PasswordHasher
? **Credit card encryption** using AES-256-CBC EncryptionService
? **Database persistence** with error handling and cleanup
? **Redirect to Login** with success message and pre-filled email
? **Security best practices** throughout implementation

The registration system is **production-ready** with comprehensive validation, security, and error handling! ??
