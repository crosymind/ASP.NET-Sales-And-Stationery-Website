using System;
using System.Security.Cryptography;
using System.Text;

namespace QuanLyKho.Common
{
    public static class SecurityHelper
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: 32
            );

            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;

            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);
                if (hashBytes.Length != 48) return false;

                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations: 100000,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: 32
                );

                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + 16] != hash[i]) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
