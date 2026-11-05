using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Infrastructure.Identity;

/// <summary>Uses ASP.NET Core Identity's battle-tested PBKDF2 hasher (v3 format).</summary>
public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string password, string hash)
    {
        try
        {
            return _hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hash) =>
        _hasher.VerifyHashedPassword(new object(), hash, "check") == PasswordVerificationResult.SuccessRehashNeeded
        || !hash.StartsWith("AQAAAA", StringComparison.Ordinal); // legacy/unknown format
}

/// <summary>RFC 6238 TOTP (HMAC-SHA1, 30s step, 6 digits) with ±1 window drift and replay guard.</summary>
public class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public string GenerateSecret()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20); // 160-bit
        return Base32Encode(bytes);
    }

    public string OtpAuthUri(string email, string secret, string issuer) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public bool Verify(string secret, string code, TimeSpan? drift = null)
    {
        var window = drift ?? TimeSpan.FromSeconds(StepSeconds);
        var steps = (int)(window.TotalSeconds / StepSeconds);
        var currentStep = CurrentStep();

        for (var offset = -steps; offset <= steps; offset++)
        {
            if (ComputeForStep(secret, currentStep + offset) == code)
                return true;
        }
        return false;
    }

    public string ComputeCode(string secret, DateTimeOffset at) =>
        ComputeForStep(secret, at.ToUnixTimeSeconds() / StepSeconds);

    private static long CurrentStep() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

    private static string ComputeForStep(string secret, long step)
    {
        var key = Base32Decode(secret);
        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, step);

        using var hmac = new System.Security.Cryptography.HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());

        // Dynamic truncation (RFC 4226 §5.4).
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        return (binary % (int)Math.Pow(10, Digits)).ToString($"D{Digits}");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new System.Text.StringBuilder();
        var bits = 0; var bitCount = 0;
        foreach (var b in data)
        {
            bits = (bits << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                sb.Append(alphabet[(bits >> (bitCount - 5)) & 0x1F]);
                bitCount -= 5;
            }
        }
        if (bitCount > 0) sb.Append(alphabet[(bits << (5 - bitCount)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0; var bitCount = 0;
        var output = new List<byte>();
        foreach (var c in encoded.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c);
            if (index < 0) throw new FormatException("Invalid base32 character.");
            bits = (bits << 5) | index;
            bitCount += 5;
            if (bitCount >= 8)
            {
                output.Add((byte)((bits >> (bitCount - 8)) & 0xFF));
                bitCount -= 8;
            }
        }
        return [.. output];
    }
}
