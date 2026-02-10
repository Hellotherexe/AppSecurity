using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using BookwormsOnline.Configuration;

namespace BookwormsOnline.Services;

/// <summary>
/// Email service interface for sending emails
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task Send2FACodeAsync(string to, string code);
    Task SendPasswordResetEmailAsync(string to, string resetUrl);
}

/// <summary>
/// Email service implementation using MailKit for SMTP
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailSettings _emailSettings;

    public EmailService(
        ILogger<EmailService> logger,
        IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailSettings = emailSettings.Value;
    }

    /// <summary>
    /// Send email using SMTP
    /// </summary>
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            
            // Connect to SMTP server
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            
            // Authenticate
            await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            
            // Send email
            await smtp.SendAsync(message);
            
            // Disconnect
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email.");
            throw;
        }
    }

    /// <summary>
    /// Send 2FA verification code via email
    /// </summary>
    public async Task Send2FACodeAsync(string to, string code)
    {
        var subject = "Your Bookworms Online Verification Code";
        var body = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .code {{ font-size: 32px; font-weight: bold; color: #0d6efd; text-align: center; 
                 padding: 20px; background-color: white; border: 2px dashed #0d6efd; 
                 letter-spacing: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #666; font-size: 12px; padding: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Bookworms Online</h1>
        </div>
        <div class='content'>
            <h2>Two-Factor Authentication</h2>
            <p>You requested a verification code to access your account.</p>
            <p>Your verification code is:</p>
            <div class='code'>{code}</div>
            <p>This code will expire in <strong>10 minutes</strong>.</p>
            <p>If you didn't request this code, please ignore this email or contact support.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply.</p>
            <p>&copy; 2024 Bookworms Online. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(to, subject, body);
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
    public async Task SendPasswordResetEmailAsync(string to, string resetUrl)
    {
        var subject = "Reset Your Bookworms Online Password";
        var body = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f8f9fa; padding: 30px; }}
        .button {{ display: inline-block; padding: 15px 30px; background-color: #0d6efd; 
                   color: white; text-decoration: none; border-radius: 5px; font-weight: bold; 
                   margin: 20px 0; }}
        .footer {{ text-align: center; color: #666; font-size: 12px; padding: 20px; }}
        .warning {{ color: #dc3545; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Bookworms Online</h1>
        </div>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>You requested to reset your password for your Bookworms Online account.</p>
            <p>Click the button below to reset your password:</p>
            <p style='text-align: center;'>
                <a href='{resetUrl}' class='button'>Reset Password</a>
            </p>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; background-color: white; padding: 10px; border: 1px solid #ddd;'>
                {resetUrl}
            </p>
            <p class='warning'>?? This link will expire in 24 hours.</p>
            <p>If you didn't request this password reset, please ignore this email. Your password will remain unchanged.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply.</p>
            <p>&copy; 2024 Bookworms Online. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(to, subject, body);
    }
}
