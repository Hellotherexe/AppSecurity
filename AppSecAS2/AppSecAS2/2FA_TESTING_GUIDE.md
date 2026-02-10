# ?? 2FA Testing Guide - Step by Step

## Quick Setup & Testing

### Prerequisites
1. ? Build successful
2. ? Database migration applied
3. ? At least one registered member in database

---

## ?? Option 1: Test Email OTP (Simplest)

### Step 1: Enable Email 2FA in Database

```sql
-- Find your member
SELECT MemberId, Email, FirstName, LastName 
FROM Members;

-- Enable Email 2FA for MemberId = 1 (adjust as needed)
UPDATE Members
SET TwoFactorEnabled = 1,
    TwoFactorType = 'Email'
WHERE MemberId = 1;

-- Verify it's enabled
SELECT MemberId, Email, TwoFactorEnabled, TwoFactorType
FROM Members
WHERE MemberId = 1;
```

### Step 2: Login

1. Navigate to `/Account/Login`
2. Enter your email and password
3. Click "Login"

### Step 3: Check Console for OTP

Since we're using the stub EmailService, the OTP will be **logged to the console/debug output**.

**In Visual Studio:**
- Look at the **Output** window
- Select "Debug" from the dropdown
- You'll see something like:

```
=== EMAIL SENT ===
To: user@example.com
Subject: Your Bookworms Online Verification Code
Body: [HTML content with 6-digit code like 123456]
==================
```

**Look for the 6-digit code** in the HTML body.

### Step 4: Enter Code on Verify2FA Page

1. You should be redirected to `/Account/Verify2FA`
2. Enter the 6-digit code from the console
3. Click "Verify Code"
4. Should successfully login and redirect to your member details

### Step 5: Test Failure Scenarios

**Test Wrong Code:**
```
1. Login again
2. Check console for new OTP (e.g., 123456)
3. Enter wrong code (e.g., 999999)
4. Should see: "Incorrect verification code. 2 attempt(s) remaining."
```

**Test 3 Failed Attempts:**
```
1. Login again
2. Enter wrong code 3 times
3. Should see: "Too many failed attempts. Please request a new code."
```

**Test Resend:**
```
1. Login again
2. Click "Resend Code" button
3. Check console for new OTP
4. Enter the new code
5. Should work
```

---

## ?? Option 2: Test TOTP (Authenticator App)

### Step 1: Generate TOTP Secret

Run this C# code in a test method or debug console:

```csharp
using OtpNet;

var key = KeyGeneration.GenerateRandomKey(20);
var secret = Base32Encoding.ToString(key);
Console.WriteLine($"TOTP Secret: {secret}");
// Example output: JBSWY3DPEHPK3PXP
```

Or use an online generator: https://www.codetwo.com/freeware/google-authenticator/

### Step 2: Enable TOTP in Database

```sql
UPDATE Members
SET TwoFactorEnabled = 1,
    TwoFactorType = 'TOTP',
    TotpSecretKey = 'JBSWY3DPEHPK3PXP'  -- Use your generated secret
WHERE MemberId = 1;
```

### Step 3: Add to Authenticator App

**Option A: Manual Entry**
1. Open Google Authenticator (or any authenticator app)
2. Click "+" ? "Enter a setup key"
3. Account name: `Bookworms - user@example.com`
4. Key: `JBSWY3DPEHPK3PXP` (your secret)
5. Type of key: Time based
6. Click "Add"

**Option B: QR Code (Implement Enable2FA UI first)**
- Scan QR code with authenticator app
- Code automatically added

### Step 4: Login and Verify

1. Navigate to `/Account/Login`
2. Enter email and password
3. Redirected to `/Account/Verify2FA`
4. Open authenticator app
5. Enter the 6-digit code shown
6. Click "Verify Code"
7. Should successfully login

### Step 5: Test Time Window

TOTP codes change every 30 seconds. Test:

1. Note current code (e.g., 123456)
2. Wait for code to change (new code: 789012)
3. Try using OLD code (123456)
4. Should still work (±30 second window)
5. Try code from 2+ minutes ago
6. Should fail

---

## ?? Debug & Troubleshooting

### Check Session Variables

Add breakpoint in `AccountController.Verify2FA` GET method:

```csharp
var memberId = HttpContext.Session.GetInt32("2FA_MemberId");
var type = HttpContext.Session.GetString("2FA_Type");
var email = HttpContext.Session.GetString("2FA_Email");

// Inspect these values in debugger
```

### Check Database State

```sql
-- View member's 2FA settings
SELECT MemberId, Email, TwoFactorEnabled, TwoFactorType, 
       EmailOtp, OtpGeneratedAtUtc, Failed2FAAttempts
FROM Members
WHERE MemberId = 1;

-- View audit logs
SELECT TOP 20 * 
FROM AuditLogs
WHERE MemberId = 1
ORDER BY TimestampUtc DESC;
```

### Enable Detailed Logging

In `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "BookwormsOnline.Services.TwoFactorAuthService": "Debug",
      "BookwormsOnline.Services.EmailService": "Debug"
    }
  }
}
```

### Common Issues

**Issue: Session Lost**
- Symptom: Redirected to login when accessing Verify2FA
- Cause: Session timeout or not configured
- Fix: Check session configuration in Program.cs

**Issue: Email OTP Not Showing**
- Symptom: No OTP in console
- Cause: EmailService not called or logging not visible
- Fix: Check Output window, select "Debug" source

**Issue: TOTP Code Always Wrong**
- Symptom: Valid code from app rejected
- Cause: System time out of sync
- Fix: Sync system clock with NTP server

**Issue: reCAPTCHA Failed**
- Symptom: "Bot detection failed"
- Cause: Missing/incorrect reCAPTCHA keys
- Fix: Use test keys or configure real keys

---

## ?? Expected Audit Log Events

After testing, you should see these events in `AuditLogs`:

```
LoginSuccess              (password verified)
    ?
2FARequired               (2FA verification started)
    ?
EmailOtpGenerated         (OTP created - Email only)
    ?
2FAVerificationFailed     (wrong codes entered)
    ?
2FAVerificationSuccess    (correct code entered)
    ?
LoginSuccess              (login completed)
```

**Query:**
```sql
SELECT Action, Details, TimestampUtc
FROM AuditLogs
WHERE MemberId = 1
  AND TimestampUtc >= DATEADD(hour, -1, GETUTCDATE())
ORDER BY TimestampUtc DESC;
```

---

## ? Test Checklist

### Email OTP
- [ ] Login redirects to Verify2FA
- [ ] OTP visible in console/logs
- [ ] Valid code accepts
- [ ] Invalid code rejects with attempts remaining
- [ ] 3 failed attempts locks out
- [ ] Resend generates new code
- [ ] Resend button has 60s cooldown
- [ ] Code expires after 10 minutes
- [ ] Session clears after successful verification

### TOTP
- [ ] Login redirects to Verify2FA
- [ ] Valid code from authenticator app accepts
- [ ] Invalid code rejects
- [ ] Code in time window (±30s) accepts
- [ ] Old code (>1 min) rejects
- [ ] 3 failed attempts locks out
- [ ] No resend button shown (TOTP only)

### Security
- [ ] reCAPTCHA verification required
- [ ] CSRF token validation
- [ ] Session timeout redirects to login
- [ ] Direct access to Verify2FA without session ? login

### Audit Logging
- [ ] 2FARequired logged
- [ ] EmailOtpGenerated logged (Email only)
- [ ] 2FAVerificationSuccess logged
- [ ] 2FAVerificationFailed logged
- [ ] LoginSuccess logged after verification

---

## ?? Quick Test Script

Run these SQL commands for quick testing:

```sql
-- 1. Enable Email 2FA for test user
UPDATE Members
SET TwoFactorEnabled = 1, TwoFactorType = 'Email'
WHERE Email = 'test@example.com';

-- 2. Login via UI, check console for OTP

-- 3. View current OTP in database
SELECT Email, EmailOtp, OtpGeneratedAtUtc, Failed2FAAttempts
FROM Members
WHERE Email = 'test@example.com';

-- 4. Manually set OTP for testing
UPDATE Members
SET EmailOtp = '123456',
    OtpGeneratedAtUtc = GETUTCDATE(),
    Failed2FAAttempts = 0
WHERE Email = 'test@example.com';

-- 5. Test with code '123456'

-- 6. View audit logs
SELECT TOP 10 Action, Details, TimestampUtc
FROM AuditLogs
WHERE MemberId IN (SELECT MemberId FROM Members WHERE Email = 'test@example.com')
ORDER BY TimestampUtc DESC;

-- 7. Disable 2FA when done testing
UPDATE Members
SET TwoFactorEnabled = 0,
    TwoFactorType = NULL,
    TotpSecretKey = NULL,
    EmailOtp = NULL,
    OtpGeneratedAtUtc = NULL,
    Failed2FAAttempts = 0
WHERE Email = 'test@example.com';
```

---

## ?? Production Readiness

Before deploying to production:

### 1. Implement Real Email Service
Replace stub EmailService with actual SMTP/SendGrid/AWS SES

### 2. Test Email Delivery
- Send real emails
- Check spam folder
- Verify formatting
- Test on mobile devices

### 3. Performance Testing
- Test with multiple concurrent users
- Check database query performance
- Monitor session storage

### 4. Security Review
- Review all audit logs
- Test rate limiting
- Verify session security
- Check HTTPS enforcement

### 5. User Documentation
- Create 2FA setup guide for users
- Document troubleshooting steps
- Train support team

---

**You're now ready to test the complete 2FA implementation!** ????
