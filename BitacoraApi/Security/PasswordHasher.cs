using System.Security.Cryptography;
using System.Text;

namespace BitacoraApi.Security
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32;  // 256 bits
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var key = pbkdf2.GetBytes(KeySize);

            var result = new byte[1 + 4 + SaltSize + KeySize];
            result[0] = 0x01; // version
            BitConverter.GetBytes(Iterations).CopyTo(result, 1);
            salt.CopyTo(result, 5);
            key.CopyTo(result, 5 + SaltSize);

            return Convert.ToBase64String(result);
        }

        public static bool Verify(string password, string hash)
        {
            var bytes = Convert.FromBase64String(hash);
            if (bytes.Length < 1 + 4 + SaltSize + KeySize)
                return false;

            var version = bytes[0];
            if (version != 0x01)
                return false;

            var iterations = BitConverter.ToInt32(bytes, 1);
            var salt = new byte[SaltSize];
            Buffer.BlockCopy(bytes, 5, salt, 0, SaltSize);
            var storedKey = new byte[KeySize];
            Buffer.BlockCopy(bytes, 5 + SaltSize, storedKey, 0, KeySize);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var key = pbkdf2.GetBytes(KeySize);

            return CryptographicOperations.FixedTimeEquals(storedKey, key);
        }
    }
}

