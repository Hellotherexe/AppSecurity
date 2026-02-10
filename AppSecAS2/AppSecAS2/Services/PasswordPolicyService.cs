using System.Text.RegularExpressions;

namespace BookwormsOnline.Services;

public class PasswordPolicyService
{
    private const int MinimumLength = 12;

    public PasswordValidationResult ValidatePassword(string password)
    {
        var result = new PasswordValidationResult();

        if (string.IsNullOrEmpty(password))
        {
            result.ErrorMessages.Add("Password is required.");
            return result;
        }

        // Check minimum length
        if (password.Length < MinimumLength)
        {
            result.ErrorMessages.Add($"Password must be at least {MinimumLength} characters long.");
        }

        // Check for at least one lowercase letter
        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            result.ErrorMessages.Add("Password must contain at least one lowercase letter.");
        }

        // Check for at least one uppercase letter
        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            result.ErrorMessages.Add("Password must contain at least one uppercase letter.");
        }

        // Check for at least one digit
        if (!Regex.IsMatch(password, @"\d"))
        {
            result.ErrorMessages.Add("Password must contain at least one digit.");
        }

        // Check for at least one special character
        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            result.ErrorMessages.Add("Password must contain at least one special character.");
        }

        result.IsValid = result.ErrorMessages.Count == 0;
        return result;
    }

    public string GetPasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Very Weak";
        }

        int rulesSatisfied = 0;

        // Check each rule
        if (password.Length >= MinimumLength)
            rulesSatisfied++;

        if (Regex.IsMatch(password, @"[a-z]"))
            rulesSatisfied++;

        if (Regex.IsMatch(password, @"[A-Z]"))
            rulesSatisfied++;

        if (Regex.IsMatch(password, @"\d"))
            rulesSatisfied++;

        if (Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            rulesSatisfied++;

        // Return strength based on rules satisfied
        return rulesSatisfied switch
        {
            5 => "Strong",
            4 => "Medium",
            3 => "Weak",
            _ => "Very Weak"
        };
    }

    public PasswordStrengthDetails GetPasswordStrengthDetails(string password)
    {
        var details = new PasswordStrengthDetails
        {
            Strength = GetPasswordStrength(password)
        };

        if (string.IsNullOrEmpty(password))
        {
            return details;
        }

        details.HasMinimumLength = password.Length >= MinimumLength;
        details.HasLowercase = Regex.IsMatch(password, @"[a-z]");
        details.HasUppercase = Regex.IsMatch(password, @"[A-Z]");
        details.HasDigit = Regex.IsMatch(password, @"\d");
        details.HasSpecialCharacter = Regex.IsMatch(password, @"[^a-zA-Z0-9]");

        return details;
    }
}

public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ErrorMessages { get; set; } = new List<string>();
}

public class PasswordStrengthDetails
{
    public string Strength { get; set; } = string.Empty;
    public bool HasMinimumLength { get; set; }
    public bool HasLowercase { get; set; }
    public bool HasUppercase { get; set; }
    public bool HasDigit { get; set; }
    public bool HasSpecialCharacter { get; set; }
}
