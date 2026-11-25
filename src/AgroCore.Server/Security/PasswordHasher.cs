using System;
using System.Security.Cryptography;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Pasahitzen hash segurua PBKDF2 bidez (SHA-256, 100.000 iterazio eta ausazko gatza).
    ///
    /// Gordetako formatua:  PBKDF2$100000$&lt;gatza base64-n&gt;$&lt;hasha base64-n&gt;
    ///
    /// - Gatzari esker, pasahitz bera duten bi erabiltzailek hash desberdinak dituzte.
    /// - Iterazio askorekin, oso motela da pasahitzak indar gordinez probatzea.
    /// - Pasahitza ez da inoiz testu arruntean gordetzen.
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return "PBKDF2$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            string[] parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2")
            {
                return false;
            }

            try
            {
                int iterations = int.Parse(parts[1]);
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

                // Denbora konstanteko konparazioa: ez du pistarik ematen huts egiten behar duen denboragatik.
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Ausazko gako irakurgarri bat sortzen du (makinen flota-gakoetarako).</summary>
        public static string GenerateKey()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(9);
            string text = Convert.ToBase64String(bytes).Replace('+', 'X').Replace('/', 'Y');
            return "AGRO-" + text;
        }

        /// <summary>Ausazko saio-token bat sortzen du.</summary>
        public static string GenerateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }
    }
}
