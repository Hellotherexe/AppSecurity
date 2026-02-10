namespace BookwormsOnline.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; } = 500;

    /// <summary>
    /// Error title to display
    /// </summary>
    public string ErrorTitle { get; set; } = "Error";

    /// <summary>
    /// User-friendly error message (no sensitive information)
    /// </summary>
    public string ErrorMessage { get; set; } = "An error occurred while processing your request.";
}
