using System;
using System.Security.Cryptography;
using System.Text;

namespace EnterprisePOS.Data
{
    // Matches the teacher's PosDB script:
    //   PasswordHash = SHA-256 of (Salt + Password), written as 64 uppercase hex characters
    //   Salt         = 32 hex characters stored in Users.Salt
    public static class PasswordHasher
    {
        // New random salt for a new user (32 hex characters)
        public static string NewSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return ToHex(bytes);
        }

        // 64 hex characters, same result as HASHBYTES('SHA2_256', salt + password) in SQL
        public static string Hash(string salt, string password)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password)));
            }
        }

        public static bool Verify(string password, string salt, string expectedHash)
        {
            if (password == null || salt == null || expectedHash == null) return false;

            var actual = Hash(salt, password);
            if (actual.Length != expectedHash.Length) return false;

            // Look at every character, so the time taken does not show where they first differ
            int difference = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                difference |= char.ToUpperInvariant(actual[i]) ^ char.ToUpperInvariant(expectedHash[i]);
            }
            return difference == 0;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}