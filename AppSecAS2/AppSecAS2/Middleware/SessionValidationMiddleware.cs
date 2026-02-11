using BookwormsOnline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace BookwormsOnline.Middleware;

/// <summary>
/// Middleware to detect multiple concurrent logins from different devices/browsers
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        // Only check authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var memberIdClaim = context.User.FindFirst("MemberId")?.Value;
            
            if (int.TryParse(memberIdClaim, out int memberId))
            {
                // Get current session ID from HttpContext.Session
                var currentSessionId = context.Session.GetString("SessionId");

                // Get member's stored session ID from database
                var member = await dbContext.Members
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MemberId == memberId);

                // If session ID missing (expired) OR member not found OR stored session differs -> sign out
                if (string.IsNullOrEmpty(currentSessionId) || member == null || member.CurrentSessionId != currentSessionId)
                {
                    _logger.LogWarning(
                        "Session invalid for MemberId: {MemberId}. SessionId from session: {SessionId}, stored: {Stored}",
                        memberId, currentSessionId ?? "<null>", member?.CurrentSessionId ?? "<null>");

                    // Sign out the user
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    // Clear session
                    context.Session.Clear();

                    // Redirect to login with message
                    context.Response.Redirect(
                        "/Account/Login?message=Session expired or logged in from another device.");
                    return;
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class SessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionValidationMiddleware>();
    }
}
