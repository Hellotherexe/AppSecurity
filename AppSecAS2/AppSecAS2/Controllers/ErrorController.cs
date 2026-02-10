using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BookwormsOnline.Models;
using System.Diagnostics;

namespace BookwormsOnline.Controllers;

/// <summary>
/// Controller for handling error pages
/// </summary>
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// General error page (500 Internal Server Error)
    /// </summary>
    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (exceptionFeature != null)
        {
            var exception = exceptionFeature.Error;
            var path = exceptionFeature.Path;

            _logger.LogError(exception, 
                "Unhandled exception occurred on path: {Path}", path);
        }

        var errorViewModel = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = 500,
            ErrorMessage = "An unexpected error occurred. Please try again later.",
            ErrorTitle = "Internal Server Error"
        };

        return View("Error", errorViewModel);
    }

    /// <summary>
    /// Status code error pages (404, 403, etc.)
    /// </summary>
    [Route("Error/{statusCode}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int statusCode)
    {
        var statusCodeResult = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        
        if (statusCodeResult != null)
        {
            _logger.LogWarning(
                "Status Code {StatusCode} on path: {Path}", 
                statusCode, statusCodeResult.OriginalPath);
        }

        var errorViewModel = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = statusCode
        };

        // Return specific view if it exists, otherwise use generic Error view
        string viewName;
        
        switch (statusCode)
        {
            case 404:
                errorViewModel.ErrorTitle = "Page Not Found";
                errorViewModel.ErrorMessage = "The page you are looking for could not be found. It may have been moved or deleted.";
                viewName = "404";
                break;

            case 403:
                errorViewModel.ErrorTitle = "Access Denied";
                errorViewModel.ErrorMessage = "You do not have permission to access this resource.";
                viewName = "403";
                break;

            case 500:
                errorViewModel.ErrorTitle = "Internal Server Error";
                errorViewModel.ErrorMessage = "An unexpected error occurred. Please try again later.";
                viewName = "500";
                break;

            case 401:
                errorViewModel.ErrorTitle = "Unauthorized";
                errorViewModel.ErrorMessage = "You must be logged in to access this resource.";
                viewName = "Error";
                break;

            case 400:
                errorViewModel.ErrorTitle = "Bad Request";
                errorViewModel.ErrorMessage = "The request could not be understood by the server.";
                viewName = "Error";
                break;

            default:
                errorViewModel.ErrorTitle = $"Error {statusCode}";
                errorViewModel.ErrorMessage = "An error occurred while processing your request.";
                viewName = "Error";
                break;
        }

        return View(viewName, errorViewModel);
    }
}
