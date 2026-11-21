using System;
using System.Security.Cryptography;

namespace FlockManagement.Common.Helpers
{
    /// <summary>
    /// PBKDF2 (HMAC-SHA256) password hashing.
    /// Stored format: PBKDF2-SHA256$iterations$saltBase64$hashBase64
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int DefaultIterations = 100000;

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            var hash = Derive(password, salt, DefaultIterations);
            return string.Join("$", Prefix, DefaultIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;

            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return false;

            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations < 1000) return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations, expected.Length);
            return FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashSize)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(length);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
