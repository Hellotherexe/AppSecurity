using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using BookwormsOnline.Data;
using BookwormsOnline.Services;
using BookwormsOnline.Configuration;
using BookwormsOnline.Models;
using BookwormsOnline.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure session with 15-minute idle timeout
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Configure cookie-based authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Add HttpContextAccessor for accessing HttpContext in services
builder.Services.AddHttpContextAccessor();

// Configure encryption settings from appsettings.json
builder.Services.Configure<EncryptionSettings>(
    builder.Configuration.GetSection("Encryption"));

// Configure reCAPTCHA settings from appsettings.json
builder.Services.Configure<ReCaptchaSettings>(
    builder.Configuration.GetSection("ReCaptcha"));

// Configure email settings from appsettings.json
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

// Register services
builder.Services.AddScoped<PasswordPolicyService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<IPasswordHasher<Member>, PasswordHasher<Member>>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<PasswordManagementService>();
builder.Services.AddScoped<TwoFactorAuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register HttpClient for RecaptchaService
builder.Services.AddHttpClient<RecaptchaService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    // Enable custom error pages even in development
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}
else
{
    // Custom error handling for production
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session (must be before authentication)
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Add custom middleware for session validation (multiple login detection)
app.UseSessionValidation();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.Run();
