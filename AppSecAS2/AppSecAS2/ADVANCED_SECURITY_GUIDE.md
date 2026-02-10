# Advanced Security Features Implementation Guide

## Overview
This guide covers the implementation of custom error pages and advanced password management features including password history, age restrictions, change password, and password reset functionality.

---

## Part 1: Custom Error Pages

### Features Implemented

? **ErrorController** - Handles all error responses
? **Status Code Pages** - 404, 403, 401, 400, 500
? **User-Friendly Messages** - No sensitive information exposed
? **Custom Error View** - Professional error display with actions
? **Request ID Tracking** - For support and debugging

### Error Handling Configuration

#### Program.cs Configuration

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // Custom error handling for production
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}
```

**Configuration Details:**
- `UseExceptionHandler("/Error")` - Handles unhandled exceptions
- `UseStatusCodePagesWithReExecute("/Error/{0}")` - Handles HTTP status codes
- Only enabled in production (not development)

### Error Codes Handled

| Status Code | Error Title | Description |
|-------------|-------------|-------------|
| **400** | Bad Request | Invalid request format |
| **401** | Unauthorized | Authentication required |
| **403** | Access Denied | Insufficient permissions |
| **404** | Page Not Found | Resource doesn't exist |
| **500** | Internal Server Error | Unexpected server error |

### ErrorController Implementation

```csharp
[Route("Error")]
public IActionResult Index()
{
    var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
    
    // Log exception but don't expose to user
    _logger.LogError(exception, "Unhandled exception on path: {Path}", path);
    
    // Return user-friendly error view
    return View("Error", errorViewModel);
}

[Route("Error/{statusCode}")]
public IActionResult StatusCode(int statusCode)
{
    // Handle specific status codes with appropriate messages
    return View("Error", errorViewModel);
}
```

### Error View Features

- ? **User-Friendly Messages** - Clear explanation of the error
- ? **Helpful Actions** - Home button, Go Back button
- ? **Context-Specific Help** - Different suggestions per error type
- ? **Request ID** - For support/debugging
- ? **Professional Design** - Bootstrap card layout with icons
- ? **No Stack Traces** - Never exposes implementation details

---

## Part 2: Advanced Password Policies

### Features Implemented

? **Password History** - Track last N passwords
? **Password Age** - Minimum and maximum password age
? **Change Password** - Authenticated users only
? **Forgot Password** - Email-based reset flow
? **Reset Password** - Secure token validation
? **Policy Enforcement** - All changes follow same rules
? **Audit Logging** - All password events logged

### Configuration Constants

```csharp
public const int MinPasswordAgeMinutes = 1;      // Min 1 minute between changes
public const int MaxPasswordAgeDays = 90;        // Max 90 days before required change
public const int PasswordHistoryCount = 2;       // Cannot reuse last 2 passwords
public const int ResetTokenExpirationHours = 24; // Reset tokens valid 24 hours
```

**Customize These Values:**
- Adjust `MinPasswordAgeMinutes` for your security requirements
- Set `MaxPasswordAgeDays` to enforce periodic password changes
- Increase `PasswordHistoryCount` to prevent more password reuse
- Modify `ResetTokenExpirationHours` for token lifetime

---

## Database Schema Changes

### PasswordHistory Table

```sql
CREATE TABLE PasswordHistories (
    Id INT PRIMARY KEY IDENTITY(1,1),
    MemberId INT NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    ChangedAtUtc DATETIME2 NOT NULL,
    CONSTRAINT FK_PasswordHistory_Member FOREIGN KEY (MemberId) 
        REFERENCES Members(MemberId) ON DELETE CASCADE
);

CREATE INDEX IX_PasswordHistory_MemberId ON PasswordHistories(MemberId);
CREATE INDEX IX_PasswordHistory_ChangedAtUtc ON PasswordHistories(ChangedAtUtc);
```

**Purpose:** Stores hashed passwords for history comparison

### PasswordResetToken Table

```sql
CREATE TABLE PasswordResetTokens (
    Id INT PRIMARY KEY IDENTITY(1,1),
    MemberId INT NOT NULL,
    Token NVARCHAR(100) NOT NULL UNIQUE,
    CreatedAtUtc DATETIME2 NOT NULL,
    ExpiresAtUtc DATETIME2 NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0,
    UsedAtUtc DATETIME2 NULL,
    CONSTRAINT FK_PasswordResetToken_Member FOREIGN KEY (MemberId) 
        REFERENCES Members(MemberId) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_PasswordResetToken_Token_Unique ON PasswordResetTokens(Token);
CREATE INDEX IX_PasswordResetToken_MemberId ON PasswordResetTokens(MemberId);
CREATE INDEX IX_PasswordResetToken_ExpiresAtUtc ON PasswordResetTokens(ExpiresAtUtc);
```

**Purpose:** Stores secure reset tokens for forgot password flow

### Member Table Updates

```sql
ALTER TABLE Members ADD
    PasswordChangedAtUtc DATETIME2 NULL;
```

**Purpose:** Tracks when password was last changed

---

## Password Management Service

### PasswordManagementService.ChangePasswordAsync()

**Flow:**
1. ? Find member and load password history
2. ? Verify current password is correct
3. ? Validate new password meets policy
4. ? Check minimum password age (prevent rapid changes)
5. ? Check password history (prevent reuse)
6. ? Save old password to history
7. ? Hash and update new password
8. ? Update PasswordChangedAtUtc timestamp
9. ? Log "PasswordChanged" event
10. ? Return success/failure result

**Example Usage:**
```csharp
var result = await _passwordManagementService.ChangePasswordAsync(
    memberId, 
    currentPassword, 
    newPassword);

if (result.Success)
{
    // Password changed successfully
}
else
{
    // Display result.ErrorMessage
    // Display result.PolicyErrors if any
}
```

### Password History Validation

```csharp
// Get last N passwords
var recentPasswords = await _context.PasswordHistories
    .Where(p => p.MemberId == memberId)
    .OrderByDescending(p => p.ChangedAtUtc)
    .Take(PasswordHistoryCount)
    .ToListAsync();

// Compare new password against history
foreach (var historicalPassword in recentPasswords)
{
    var verification = _passwordHasher.VerifyHashedPassword(
        member, historicalPassword.PasswordHash, newPassword);
    
    if (verification != PasswordVerificationResult.Failed)
    {
        // Password matches history - reject
        return error;
    }
}
```

### Minimum Password Age Check

```csharp
if (member.PasswordChangedAtUtc.HasValue)
{
    var timeSinceLastChange = DateTime.UtcNow - member.PasswordChangedAtUtc.Value;
    
    if (timeSinceLastChange.TotalMinutes < MinPasswordAgeMinutes)
    {
        var remaining = MinPasswordAgeMinutes - timeSinceLastChange.TotalMinutes;
        return error("Wait {remaining} more minutes");
    }
}
```

---

## Password Reset Flow

### Step 1: Generate Reset Token

```csharp
var token = await _passwordManagementService.GeneratePasswordResetTokenAsync(email);
```

**Process:**
1. Find member by email
2. Generate secure token (Base64 GUID)
3. Create PasswordResetToken record
4. Set expiration (24 hours)
5. Save to database
6. Log "PasswordResetTokenGenerated" event
7. Return token

**Token Format:**
```
Original: 12345678-1234-1234-1234-123456789abc
Base64:   EjRWeBI0EjRWeBI0EjRWeQ==
Clean:    EjRWeBI0EjRWeBI0EjRWeQ
```

### Step 2: Send Reset Email

```csharp
// Build reset URL
var resetUrl = Url.Action(
    "ResetPassword", 
    "Account", 
    new { token = token }, 
    protocol: Request.Scheme);

// Example: https://yourdomain.com/Account/ResetPassword?token=EjRWeBI0...

// Send email (implement IEmailService)
await _emailService.SendAsync(
    to: member.Email,
    subject: "Reset Your Password",
    body: $"Click here to reset your password: {resetUrl}");
```

**Email Template:**
```html
<h2>Password Reset Request</h2>
<p>You requested to reset your password for Bookworms Online.</p>
<p>Click the button below to reset your password:</p>
<a href="{resetUrl}" style="...">Reset Password</a>
<p>This link will expire in 24 hours.</p>
<p>If you didn't request this, please ignore this email.</p>
```

### Step 3: Reset Password with Token

```csharp
var result = await _passwordManagementService.ResetPasswordWithTokenAsync(
    token, 
    newPassword);
```

**Validation Process:**
1. ? Find token in database
2. ? Check token not expired
3. ? Check token not already used
4. ? Load member and password history
5. ? Validate new password meets policy
6. ? Check password history (no reuse)
7. ? Save old password to history
8. ? Hash and update new password
9. ? Update PasswordChangedAtUtc
10. ? Mark token as used
11. ? Log "PasswordReset" event
12. ? Return success/failure

---

## Security Considerations

### Token Security

? **Cryptographically Random** - Generated from GUID
? **URL-Safe Encoding** - Base64 with replacements
? **Unique Constraint** - Database ensures uniqueness
? **Expiration** - Tokens expire after 24 hours
? **One-Time Use** - Tokens marked as used
? **Database Storage** - Tokens stored securely

### Password History Security

? **Hashed Storage** - History stores hashed passwords
? **Same Hashing** - Uses PasswordHasher<Member>
? **Cascade Delete** - Deleted when member deleted
? **Index Optimization** - Fast history queries

### Rate Limiting

? **Minimum Age** - Prevents rapid password changes
? **Token Expiration** - Limits reset window
? **Failed Attempts** - Logged for monitoring
? **Audit Logging** - All events tracked

---

## Audit Log Events

New events for password management:

| Event | Description |
|-------|-------------|
| **PasswordChanged** | User changed password successfully |
| **PasswordChangeFailed** | Password change failed |
| **PasswordResetTokenGenerated** | Reset token created |
| **PasswordResetTokenExpired** | Attempted to use expired token |
| **PasswordResetTokenReused** | Attempted to reuse token |
| **PasswordReset** | Password reset successfully |

---

## Migration Commands

### Create Migration

```powershell
Add-Migration AddAdvancedPasswordFeatures
```

Or:

```bash
dotnet ef migrations add AddAdvancedPasswordFeatures
```

### Apply Migration

```powershell
Update-Database
```

Or:

```bash
dotnet ef database update
```

### What Gets Created

1. **PasswordHistories** table
2. **PasswordResetTokens** table
3. **PasswordChangedAtUtc** column in Members
4. Indexes for performance
5. Foreign key relationships

---

## Password Age Enforcement

### Check if Password Expired

```csharp
public async Task<bool> IsPasswordExpiredAsync(int memberId)
{
    var member = await _context.Members.FindAsync(memberId);
    
    if (!member.PasswordChangedAtUtc.HasValue)
    {
        // Never changed - check against creation date
        var daysSinceCreation = (DateTime.UtcNow - member.CreatedAt).TotalDays;
        return daysSinceCreation > MaxPasswordAgeDays;
    }

    var daysSinceChange = (DateTime.UtcNow - member.PasswordChangedAtUtc.Value).TotalDays;
    return daysSinceChange > MaxPasswordAgeDays;
}
```

### Force Password Change

**Option 1: Middleware**
```csharp
if (User.IsAuthenticated)
{
    var isExpired = await _passwordManagementService.IsPasswordExpiredAsync(memberId);
    if (isExpired)
    {
        return RedirectToAction("ChangePassword", "Account", 
            new { returnUrl = Request.Path });
    }
}
```

**Option 2: Authorize Attribute**
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePasswordNotExpiredAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next)
    {
        // Check password age
        // Redirect if expired
    }
}
```

---

## Testing Scenarios

### Change Password Tests

- [ ] Valid change with correct current password ? Success
- [ ] Invalid current password ? Error
- [ ] New password doesn't meet policy ? Error with details
- [ ] Change before minimum age ? Error with wait time
- [ ] Reuse last password ? Error about history
- [ ] Reuse 2nd to last password ? Error about history
- [ ] Valid change after minimum age ? Success

### Forgot Password Tests

- [ ] Request reset for valid email ? Token generated
- [ ] Request reset for invalid email ? Generic message
- [ ] Multiple reset requests ? Latest token valid

### Reset Password Tests

- [ ] Valid token with valid password ? Success
- [ ] Expired token ? Error message
- [ ] Already used token ? Error message
- [ ] Invalid token ? Error message
- [ ] Reuse password from history ? Error
- [ ] Password doesn't meet policy ? Error

### Error Page Tests

- [ ] Navigate to non-existent URL ? 404 page
- [ ] Access restricted resource ? 403 page
- [ ] Access without authentication ? 401 redirect
- [ ] Cause server error ? 500 page (don't expose stack trace)
- [ ] All error pages use main layout ? Consistent UI

---

## Files Created/Modified

### New Files

1. ? `Controllers/ErrorController.cs`
2. ? `Models/PasswordHistory.cs`
3. ? `Models/PasswordResetToken.cs`
4. ? `Models/PasswordViewModels.cs`
5. ? `Services/PasswordManagementService.cs`

### Modified Files

1. ? `Models/ErrorViewModel.cs` - Added status code and messages
2. ? `Models/Member.cs` - Added PasswordChangedAtUtc and navigation properties
3. ? `Data/ApplicationDbContext.cs` - Added new DbSets and relationships
4. ? `Program.cs` - Added error handling and service registration
5. ? `Views/Shared/Error.cshtml` - Complete redesign with user-friendly messages

---

## Summary

### Custom Error Pages

? **Professional Error Handling** - User-friendly messages
? **No Information Disclosure** - Stack traces hidden
? **Helpful Actions** - Home, Back buttons
? **Request Tracking** - Request ID for support
? **Status Code Handling** - 400, 401, 403, 404, 500

### Advanced Password Features

? **Password History** - Last 2 passwords tracked
? **Minimum Age** - 1 minute between changes
? **Maximum Age** - 90 days before expiration
? **Change Password** - Full validation flow
? **Forgot Password** - Secure token generation
? **Reset Password** - Token validation and expiration
? **Audit Logging** - All password events logged
? **Security** - Hashed storage, one-time tokens

The application now has enterprise-grade password management and error handling! ???
