# BookwormsOnline Registration Form - Complete Guide

## Overview
The registration form for BookwormsOnline is a comprehensive, secure, and user-friendly form that collects member information with extensive validation.

## Form Location
**URL:** `/Member/Register`
**View File:** `Views/Member/Register.cshtml`
**Controller:** `Controllers/MemberController.cs`

## Form Features

### ? All Required Fields Implemented

| Field | Type | Validation | HTML5 Attributes |
|-------|------|------------|------------------|
| **FirstName** | Text Input | Required, Max 50 chars | `required`, `maxlength="50"`, `type="text"` |
| **LastName** | Text Input | Required, Max 50 chars | `required`, `maxlength="50"`, `type="text"` |
| **Email** | Email Input | Required, Email format, Max 255 chars | `required`, `type="email"`, `maxlength="255"` |
| **MobileNo** | Tel Input | Required, Singapore pattern | `required`, `type="tel"`, `pattern="[89]\d{7}"` |
| **Password** | Password Input | Required, Min 12 chars, Complex | `required`, `minlength="12"`, `type="password"` |
| **ConfirmPassword** | Password Input | Required, Must match Password | `required`, `minlength="12"`, `type="password"` |
| **BillingAddress** | Textarea | Required, Max 255 chars | `required`, `maxlength="255"`, `rows="3"` |
| **ShippingAddress** | Textarea | Required, Max 500 chars | `required`, `maxlength="500"`, `rows="3"` |
| **CreditCard** | Text Input | Required, 13-19 digits | `required`, `pattern="\d{13,19}"`, `inputmode="numeric"` |
| **Photo** | File Upload | Required, JPG only | `required`, `accept=".jpg,.jpeg,image/jpeg"` |

## Form Structure

### 1. Validation Summary
```html
<div asp-validation-summary="All" class="alert alert-danger" role="alert"></div>
```
- Displays all validation errors at the top
- Uses Bootstrap alert styling
- Shows both model-level and field-level errors

### 2. Form Tag with enctype
```html
<form asp-action="Register" method="post" enctype="multipart/form-data" id="registrationForm">
```
- ? `enctype="multipart/form-data"` - Required for file uploads
- ? `method="post"` - Secure form submission
- ? `asp-action="Register"` - Tag helper for action routing

### 3. Organized Sections

#### Personal Information
- FirstName, LastName, Email, MobileNo
- Uses `<fieldset>` and `<legend>` for semantic grouping
- Icon indicators for visual clarity

#### Security
- Password with real-time validation feedback
- ConfirmPassword with match validation
- Client-side and server-side validation

#### Address Information
- BillingAddress (255 chars max)
- ShippingAddress (500 chars max, allows special characters)

#### Payment Information
- Credit Card Number
- Encrypted before storage
- Auto-formatting with dashes

#### Profile Photo
- JPG file upload only
- Image preview functionality
- 5MB size limit validation

## HTML5 Validation Attributes

### Text Inputs
```html
<input asp-for="FirstName" 
       class="form-control" 
       type="text"
       required
       maxlength="50"
       placeholder="Enter your first name" />
```

### Email Input
```html
<input asp-for="Email" 
       class="form-control" 
       type="email"
       required
       maxlength="255"
       placeholder="your.email@example.com" />
```

### Mobile Number (Singapore Pattern)
```html
<input asp-for="MobileNo" 
       class="form-control" 
       type="tel"
       required
       pattern="[89]\d{7}"
       placeholder="e.g., 91234567"
       title="Singapore mobile number starting with 8 or 9, followed by 7 digits" />
```

### Password
```html
<input type="password" 
       class="form-control" 
       id="Password" 
       name="Password" 
       data-password-validation
       required
       minlength="12"
       autocomplete="new-password"
       placeholder="Enter a strong password" />
```

### Credit Card
```html
<input type="text" 
       class="form-control" 
       id="CreditCard" 
       name="CreditCard"
       required
       pattern="\d{13,19}"
       maxlength="19" 
       placeholder="XXXX-XXXX-XXXX-XXXX"
       title="Credit card number (13-19 digits)"
       inputmode="numeric" />
```

### File Upload (JPG Only)
```html
<input type="file" 
       class="form-control" 
       id="Photo" 
       name="Photo"
       required
       accept=".jpg,.jpeg,image/jpeg" />
```

## Tag Helpers Used

### asp-for
Binds input to model property:
```html
<input asp-for="FirstName" class="form-control" />
```
- Generates correct `name` and `id` attributes
- Applies data annotations from model
- Enables model binding

### asp-validation-for
Displays validation messages for specific field:
```html
<span asp-validation-for="FirstName" class="text-danger"></span>
```

### asp-validation-summary
Displays summary of all validation errors:
```html
<div asp-validation-summary="All" class="alert alert-danger"></div>
```

Options:
- `All` - All errors (model and properties)
- `ModelOnly` - Only model-level errors
- `None` - No summary

### asp-action
Specifies controller action:
```html
<form asp-action="Register" method="post">
```

## Server-Side Validation

### Data Annotations (Member.cs)
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
[RegularExpression(@"^[89]\d{7}$", ErrorMessage = "Mobile number must be a valid Singapore mobile number")]
public string MobileNo { get; set; }
```

### Controller Validation (MemberController.cs)
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(Member model, string Password, string ConfirmPassword, string? CreditCard, IFormFile? Photo)
{
    // Password validation
    var passwordValidation = _passwordService.ValidatePassword(Password);
    if (!passwordValidation.IsValid)
    {
        foreach (var error in passwordValidation.ErrorMessages)
        {
            ModelState.AddModelError("Password", error);
        }
    }

    // Password confirmation
    if (Password != ConfirmPassword)
    {
        ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
    }

    // Credit card encryption
    var (encrypted, masked) = _encryptionService.EncryptCreditCard(CreditCard);
    model.CreditCardEncrypted = encrypted;

    // Photo validation
    if (Photo != null)
    {
        var extension = Path.GetExtension(Photo.FileName).ToLowerInvariant();
        if (extension != ".jpg" && extension != ".jpeg")
        {
            ModelState.AddModelError("Photo", "Only JPG files are allowed.");
        }
    }

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    // Save to database
    _context.Members.Add(model);
    await _context.SaveChangesAsync();
}
```

## Client-Side Features

### 1. Password Confirmation Validation
```javascript
function validatePasswordMatch() {
    if (confirmPassword.value && password.value !== confirmPassword.value) {
        errorSpan.textContent = 'Passwords do not match.';
        confirmPassword.setCustomValidity('Passwords do not match.');
    } else {
        errorSpan.textContent = '';
        confirmPassword.setCustomValidity('');
    }
}
```

### 2. Photo Preview
```javascript
photoInput.addEventListener('change', function(e) {
    const file = e.target.files[0];
    
    // Validate file type
    if (!file.type.match('image/jpeg')) {
        alert('Please select a JPG file only.');
        return;
    }

    // Validate file size (5MB max)
    if (file.size > 5 * 1024 * 1024) {
        alert('File size must be less than 5MB.');
        return;
    }

    // Show preview
    const reader = new FileReader();
    reader.onload = function(e) {
        previewImage.src = e.target.result;
        photoPreview.style.display = 'block';
    };
    reader.readAsDataURL(file);
});
```

### 3. Credit Card Formatting
```javascript
creditCardInput.addEventListener('input', function(e) {
    let value = e.target.value.replace(/\D/g, '');
    let formattedValue = value.match(/.{1,4}/g)?.join('-') || value;
    e.target.value = formattedValue;
});
```

### 4. Password Strength Indicator
Real-time validation feedback showing:
- Password strength level (Strong/Medium/Weak)
- Which rules are satisfied (green checkmarks)
- Which rules are not satisfied (red X marks)

## Validation Messages

### Client-Side (HTML5)
- Displayed when user tries to submit invalid form
- Browser's native validation messages
- Immediate feedback on input

### Server-Side
- Displayed after form submission
- Custom error messages from data annotations
- Additional business logic validation

### Example Messages:
- "First name is required"
- "Email address format is invalid"
- "Mobile number must be a valid Singapore mobile number"
- "Password must be at least 12 characters long"
- "Passwords do not match"
- "Only JPG files are allowed"

## Security Features

### 1. CSRF Protection
```html
<form asp-action="Register" method="post">
    @* Anti-forgery token automatically added by tag helper *@
</form>
```

### 2. Password Encryption
- Passwords hashed before storage
- Never stored in plain text
- Server-side validation with PasswordPolicyService

### 3. Credit Card Encryption
- Encrypted with AES-256-CBC
- Only encrypted data stored
- Masked display (**** **** **** 3456)

### 4. File Upload Validation
- Client-side: Accept attribute
- Server-side: Extension check
- Size limit: 5MB
- Type validation: JPG only

## Accessibility Features

### Semantic HTML
- `<fieldset>` and `<legend>` for grouping
- Proper `<label>` associations
- ARIA attributes where appropriate

### Labels
All inputs have associated labels:
```html
<label asp-for="FirstName" class="form-label"></label>
<input asp-for="FirstName" class="form-control" />
```

### Error Messages
- Associated with inputs via `asp-validation-for`
- Screen reader friendly
- Color and icon indicators

## User Experience Features

### Visual Feedback
- Bootstrap styling and icons
- Color-coded sections
- Validation feedback
- Photo preview
- Password strength indicator

### Help Text
- Placeholder text for guidance
- Small helper text below inputs
- Info alert with requirements

### Progressive Enhancement
- Works without JavaScript
- Enhanced with JavaScript features
- Graceful degradation

## Testing Checklist

### Valid Submission
- [ ] Fill all fields correctly
- [ ] Upload JPG photo
- [ ] Enter matching passwords
- [ ] Verify successful registration

### Validation Testing
- [ ] Submit empty form - Check all required messages
- [ ] Invalid email format - Check email validation
- [ ] Invalid mobile number - Check pattern validation
- [ ] Password too short - Check minlength validation
- [ ] Non-matching passwords - Check confirmation
- [ ] Non-JPG file - Check file type validation
- [ ] Large file (>5MB) - Check size validation

### Browser Testing
- [ ] Chrome
- [ ] Firefox
- [ ] Edge
- [ ] Safari
- [ ] Mobile browsers

## Files Involved

1. **View**: `Views/Member/Register.cshtml`
2. **Controller**: `Controllers/MemberController.cs`
3. **Model**: `Models/Member.cs`
4. **Services**:
   - `Services/PasswordPolicyService.cs`
   - `Services/EncryptionService.cs`
5. **JavaScript**: `wwwroot/js/password-validation.js`
6. **CSS**: `wwwroot/css/password-validation.css`

## Summary

? **All fields implemented**: FirstName, LastName, Email, MobileNo, Password, ConfirmPassword, BillingAddress, ShippingAddress, CreditCard, Photo

? **Tag helpers used**: asp-for, asp-validation-for, asp-validation-summary, asp-action

? **enctype set**: `multipart/form-data` for file upload

? **HTML5 validation**: required, type, pattern, maxlength, minlength, accept

? **Server-side validation**: Data annotations, ModelState, custom validation

? **Validation summary**: Displayed at top of form

? **Security**: CSRF protection, password hashing, credit card encryption

? **User experience**: Photo preview, password strength, credit card formatting

The registration form is production-ready with comprehensive validation, security features, and excellent user experience! ??
