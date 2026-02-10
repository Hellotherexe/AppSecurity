using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using BookwormsOnline.Configuration;

namespace BookwormsOnline.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-256-CBC.
/// Implements IDisposable to ensure proper disposal of cryptographic objects.
/// </summary>
public class EncryptionService : IDisposable
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EncryptionService.
    /// </summary>
    /// <param name="encryptionSettings">Encryption configuration from appsettings.json</param>
    /// <exception cref="ArgumentException">Thrown when key or IV have invalid lengths</exception>
    public EncryptionService(IOptions<EncryptionSettings> encryptionSettings)
    {
        var settings = encryptionSettings.Value;

        if (string.IsNullOrEmpty(settings.Key))
        {
            throw new ArgumentException("Encryption key is not configured.");
        }

        if (string.IsNullOrEmpty(settings.IV))
        {
            throw new ArgumentException("Encryption IV is not configured.");
        }

        // Convert key and IV to bytes
        _key = Encoding.UTF8.GetBytes(settings.Key);
        _iv = Encoding.UTF8.GetBytes(settings.IV);

        // Validate key size (must be 32 bytes for AES-256)
        if (_key.Length != 32)
        {
            throw new ArgumentException(
                $"Invalid key size. Key must be 32 bytes (256 bits) for AES-256. Current size: {_key.Length} bytes. " +
                $"Key string length: {settings.Key.Length} characters.");
        }

        // Validate IV size (must be 16 bytes for AES block size)
        if (_iv.Length != 16)
        {
            throw new ArgumentException(
                $"Invalid IV size. IV must be 16 bytes (128 bits). Current size: {_iv.Length} bytes. " +
                $"IV string length: {settings.IV.Length} characters.");
        }
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-CBC.
    /// </summary>
    /// <param name="plaintext">The text to encrypt</param>
    /// <returns>Base64-encoded encrypted string</returns>
    /// <exception cref="ArgumentNullException">Thrown when plaintext is null</exception>
    /// <exception cref="CryptographicException">Thrown when encryption fails</exception>
    public string Encrypt(string plaintext)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        try
        {
            // Create AES instance with explicit settings
            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7; // Secure padding

                // Create encryptor
                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        // Write plaintext to the stream
                        swEncrypt.Write(plaintext);
                    }

                    // Convert encrypted bytes to Base64
                    byte[] encrypted = msEncrypt.ToArray();
                    return Convert.ToBase64String(encrypted);
                }
            }
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Encryption failed. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Decrypts Base64-encoded ciphertext using AES-256-CBC.
    /// </summary>
    /// <param name="cipherText">Base64-encoded encrypted string</param>
    /// <returns>Decrypted plaintext</returns>
    /// <exception cref="ArgumentNullException">Thrown when cipherText is null</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails</exception>
    public string Decrypt(string cipherText)
    {
        if (cipherText == null)
        {
            throw new ArgumentNullException(nameof(cipherText));
        }

        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        try
        {
            // Convert Base64 string to bytes
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            // Create AES instance with explicit settings
            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7; // Must match encryption padding

                // Create decryptor
                using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    // Read decrypted text from the stream
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid ciphertext format. Expected Base64 string.", ex);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Decryption failed. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Encrypts a credit card number with masking for display purposes.
    /// </summary>
    /// <param name="creditCardNumber">Credit card number to encrypt</param>
    /// <returns>Tuple containing encrypted value and masked display value</returns>
    public (string Encrypted, string Masked) EncryptCreditCard(string creditCardNumber)
    {
        if (string.IsNullOrEmpty(creditCardNumber))
        {
            throw new ArgumentNullException(nameof(creditCardNumber));
        }

        // Remove any spaces or dashes
        string cleanedNumber = creditCardNumber.Replace(" ", "").Replace("-", "");

        // Encrypt the full number
        string encrypted = Encrypt(cleanedNumber);

        // Create masked version (show only last 4 digits)
        string masked = cleanedNumber.Length >= 4
            ? $"****-****-****-{cleanedNumber.Substring(cleanedNumber.Length - 4)}"
            : "****-****-****-****";

        return (encrypted, masked);
    }

    /// <summary>
    /// Generates a cryptographically secure random key for AES-256.
    /// </summary>
    /// <returns>Base64-encoded 32-byte key</returns>
    public static string GenerateSecureKey()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] key = new byte[32]; // 256 bits
            rng.GetBytes(key);
            return Convert.ToBase64String(key);
        }
    }

    /// <summary>
    /// Generates a cryptographically secure random IV.
    /// </summary>
    /// <returns>Base64-encoded 16-byte IV</returns>
    public static string GenerateSecureIV()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] iv = new byte[16]; // 128 bits
            rng.GetBytes(iv);
            return Convert.ToBase64String(iv);
        }
    }

    /// <summary>
    /// Disposes of cryptographic resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method for cleanup.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clear sensitive data from memory
                if (_key != null)
                {
                    Array.Clear(_key, 0, _key.Length);
                }
                if (_iv != null)
                {
                    Array.Clear(_iv, 0, _iv.Length);
                }
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer to ensure cleanup even if Dispose is not called.
    /// </summary>
    ~EncryptionService()
    {
        Dispose(false);
    }
}
