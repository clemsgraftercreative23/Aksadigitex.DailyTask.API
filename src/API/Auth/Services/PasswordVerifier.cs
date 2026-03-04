using Isopoh.Cryptography.Argon2;

namespace API.Auth;

public static class PasswordVerifier
{
    public static bool Verify(string input, string hash)
    {
        if (hash.StartsWith("$2a$") || hash.StartsWith("$2b$"))
            return BCrypt.Net.BCrypt.Verify(input, hash);

        if (hash.StartsWith("$argon2", StringComparison.OrdinalIgnoreCase))
            return Argon2.Verify(hash, input);

        return false;
    }
}
