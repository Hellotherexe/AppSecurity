using BookwormsOnline.Data;
using BookwormsOnline.Models;
using Microsoft.AspNetCore.Http;

namespace BookwormsOnline.Services;

/// <summary>
/// Service for logging audit events
/// </summary>
public class AuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Log an audit event
    /// </summary>
    public async Task LogAsync(int? memberId, string action, string? details = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        var auditLog = new AuditLog
        {
            MemberId = memberId,
            Action = action,
            TimestampUtc = DateTime.UtcNow,
            IPAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
            Details = details
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get client IP address
    /// </summary>
    private string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        // Check for real IP
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Get recent failed login attempts for a member
    /// </summary>
    public async Task<int> GetRecentFailedLoginsAsync(int memberId, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
        
        return await Task.Run(() => _context.AuditLogs
            .Where(a => a.MemberId == memberId 
                     && a.Action == "LoginFailed" 
                     && a.TimestampUtc >= cutoffTime)
            .Count());
    }
}
