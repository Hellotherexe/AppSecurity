using BookwormsOnline.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookwormsOnline.Services;

/// <summary>
/// Service for verifying Google reCAPTCHA v3 tokens
/// </summary>
public class RecaptchaService
{
    private readonly ReCaptchaSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        IOptions<ReCaptchaSettings> settings,
        HttpClient httpClient,
        ILogger<RecaptchaService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Verify reCAPTCHA token with Google's API
    /// </summary>
    /// <param name="token">reCAPTCHA token from client</param>
    /// <param name="expectedAction">Expected action name (e.g., "register", "login")</param>
    /// <param name="remoteIp">Client's IP address (optional)</param>
    /// <returns>Verification result with success status and score</returns>
    public async Task<RecaptchaVerificationResult> VerifyTokenAsync(
        string token, 
        string expectedAction, 
        string? remoteIp = null)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("reCAPTCHA verification failed: Token is null or empty");
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorMessage = "reCAPTCHA token is required"
            };
        }

        try
        {
            // Build request parameters
            var parameters = new Dictionary<string, string>
            {
                { "secret", _settings.SecretKey },
                { "response", token }
            };

            if (!string.IsNullOrEmpty(remoteIp))
            {
                parameters.Add("remoteip", remoteIp);
            }

            var content = new FormUrlEncodedContent(parameters);

            // Send verification request to Google
            var response = await _httpClient.PostAsync(_settings.VerifyUrl, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Parse response
            var googleResponse = JsonSerializer.Deserialize<GoogleRecaptchaResponse>(
                jsonResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (googleResponse == null)
            {
                _logger.LogError("Failed to parse reCAPTCHA response");
                return new RecaptchaVerificationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to verify reCAPTCHA"
                };
            }

            // Check if verification succeeded
            if (!googleResponse.Success)
            {
                var errors = googleResponse.ErrorCodes != null 
                    ? string.Join(", ", googleResponse.ErrorCodes) 
                    : "Unknown error";
                
                _logger.LogWarning("reCAPTCHA verification failed: {Errors}", errors);
                
                return new RecaptchaVerificationResult
                {
                    Success = false,
                    ErrorMessage = $"reCAPTCHA verification failed: {errors}",
                    Score = googleResponse.Score
                };
            }

            // Verify action matches
            if (!string.IsNullOrEmpty(expectedAction) && 
                googleResponse.Action != expectedAction)
            {
                _logger.LogWarning(
                    "reCAPTCHA action mismatch. Expected: {Expected}, Got: {Actual}",
                    expectedAction, googleResponse.Action);
                
                return new RecaptchaVerificationResult
                {
                    Success = false,
                    ErrorMessage = "reCAPTCHA action mismatch",
                    Score = googleResponse.Score,
                    Action = googleResponse.Action
                };
            }

            // Check score threshold
            if (googleResponse.Score < _settings.MinimumScore)
            {
                _logger.LogWarning(
                    "reCAPTCHA score too low. Score: {Score}, Minimum: {Minimum}",
                    googleResponse.Score, _settings.MinimumScore);
                
                return new RecaptchaVerificationResult
                {
                    Success = false,
                    ErrorMessage = $"reCAPTCHA score too low: {googleResponse.Score:F2}",
                    Score = googleResponse.Score,
                    Action = googleResponse.Action
                };
            }

            // Success!
            _logger.LogInformation(
                "reCAPTCHA verification successful. Action: {Action}, Score: {Score}",
                googleResponse.Action, googleResponse.Score);

            return new RecaptchaVerificationResult
            {
                Success = true,
                Score = googleResponse.Score,
                Action = googleResponse.Action,
                ChallengeTimestamp = googleResponse.ChallengeTs,
                Hostname = googleResponse.Hostname
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during reCAPTCHA verification");
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorMessage = "An error occurred during reCAPTCHA verification"
            };
        }
    }

    /// <summary>
    /// Google reCAPTCHA API response model
    /// </summary>
    private class GoogleRecaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public DateTime? ChallengeTs { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}

/// <summary>
/// Result of reCAPTCHA verification
/// </summary>
public class RecaptchaVerificationResult
{
    /// <summary>
    /// Whether verification was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if verification failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Score from 0.0 (bot) to 1.0 (human)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Action name from the token
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Timestamp of the challenge
    /// </summary>
    public DateTime? ChallengeTimestamp { get; set; }

    /// <summary>
    /// Hostname where the challenge was completed
    /// </summary>
    public string? Hostname { get; set; }
}
