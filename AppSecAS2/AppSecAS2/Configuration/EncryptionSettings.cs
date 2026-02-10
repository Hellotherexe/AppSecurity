namespace BookwormsOnline.Configuration;

/// <summary>
/// Configuration settings for AES encryption.
/// Key must be 32 bytes (256 bits) for AES-256.
/// IV must be 16 bytes (128 bits) for AES block size.
/// </summary>
public class EncryptionSettings
{
    /// <summary>
    /// AES encryption key (must be 32 bytes for AES-256)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// AES initialization vector (must be 16 bytes)
    /// </summary>
    public string IV { get; set; } = string.Empty;
}
