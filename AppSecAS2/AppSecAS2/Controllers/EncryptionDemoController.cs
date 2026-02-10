using Microsoft.AspNetCore.Mvc;
using BookwormsOnline.Services;

namespace BookwormsOnline.Controllers;

public class EncryptionDemoController : Controller
{
    private readonly EncryptionService _encryptionService;

    public EncryptionDemoController(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            ViewBag.Error = "Please enter text to encrypt.";
            return View("Index");
        }

        try
        {
            string encrypted = _encryptionService.Encrypt(plaintext);
            ViewBag.Plaintext = plaintext;
            ViewBag.Encrypted = encrypted;
            ViewBag.EncryptedLength = encrypted.Length;
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Encryption failed: {ex.Message}";
        }

        return View("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            ViewBag.Error = "Please enter ciphertext to decrypt.";
            return View("Index");
        }

        try
        {
            string decrypted = _encryptionService.Decrypt(ciphertext);
            ViewBag.Ciphertext = ciphertext;
            ViewBag.Decrypted = decrypted;
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Decryption failed: {ex.Message}";
        }

        return View("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EncryptCreditCard(string creditCardNumber)
    {
        if (string.IsNullOrEmpty(creditCardNumber))
        {
            ViewBag.Error = "Please enter a credit card number.";
            return View("Index");
        }

        try
        {
            var (encrypted, masked) = _encryptionService.EncryptCreditCard(creditCardNumber);
            ViewBag.CreditCardOriginal = creditCardNumber;
            ViewBag.CreditCardEncrypted = encrypted;
            ViewBag.CreditCardMasked = masked;
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Credit card encryption failed: {ex.Message}";
        }

        return View("Index");
    }
}
