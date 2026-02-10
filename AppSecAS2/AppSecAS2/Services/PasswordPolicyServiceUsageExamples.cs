using BookwormsOnline.Services;

namespace BookwormsOnline.Tests;

// This is a demonstration of how to use the PasswordPolicyService
// You can create proper unit tests using xUnit, NUnit, or MSTest
public class PasswordPolicyServiceUsageExamples
{
    public static void RunExamples()
    {
        var passwordService = new PasswordPolicyService();

        // Example 1: Valid password
        var result1 = passwordService.ValidatePassword("MyP@ssw0rd123");
        Console.WriteLine($"Password: MyP@ssw0rd123");
        Console.WriteLine($"Is Valid: {result1.IsValid}");
        Console.WriteLine($"Strength: {passwordService.GetPasswordStrength("MyP@ssw0rd123")}");
        Console.WriteLine();

        // Example 2: Too short
        var result2 = passwordService.ValidatePassword("Short1!");
        Console.WriteLine($"Password: Short1!");
        Console.WriteLine($"Is Valid: {result2.IsValid}");
        Console.WriteLine($"Errors: {string.Join(", ", result2.ErrorMessages)}");
        Console.WriteLine($"Strength: {passwordService.GetPasswordStrength("Short1!")}");
        Console.WriteLine();

        // Example 3: Missing special character
        var result3 = passwordService.ValidatePassword("MyPassword123");
        Console.WriteLine($"Password: MyPassword123");
        Console.WriteLine($"Is Valid: {result3.IsValid}");
        Console.WriteLine($"Errors: {string.Join(", ", result3.ErrorMessages)}");
        Console.WriteLine($"Strength: {passwordService.GetPasswordStrength("MyPassword123")}");
        Console.WriteLine();

        // Example 4: Missing uppercase
        var result4 = passwordService.ValidatePassword("mypassword123!");
        Console.WriteLine($"Password: mypassword123!");
        Console.WriteLine($"Is Valid: {result4.IsValid}");
        Console.WriteLine($"Errors: {string.Join(", ", result4.ErrorMessages)}");
        Console.WriteLine($"Strength: {passwordService.GetPasswordStrength("mypassword123!")}");
        Console.WriteLine();

        // Example 5: Get detailed strength information
        var details = passwordService.GetPasswordStrengthDetails("MyP@ssw0rd123");
        Console.WriteLine($"Password: MyP@ssw0rd123");
        Console.WriteLine($"Strength Details:");
        Console.WriteLine($"  - Strength: {details.Strength}");
        Console.WriteLine($"  - Has Minimum Length (12): {details.HasMinimumLength}");
        Console.WriteLine($"  - Has Lowercase: {details.HasLowercase}");
        Console.WriteLine($"  - Has Uppercase: {details.HasUppercase}");
        Console.WriteLine($"  - Has Digit: {details.HasDigit}");
        Console.WriteLine($"  - Has Special Character: {details.HasSpecialCharacter}");
    }
}
