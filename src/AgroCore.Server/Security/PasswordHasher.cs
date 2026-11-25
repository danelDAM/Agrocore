using System;
using System.Security.Cryptography;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Hash seguro de contraseñas con PBKDF2 (SHA-256, 100.000 iteraciones y sal aleatoria).
    ///
    /// Formato guardado:  PBKDF2$100000$&lt;sal en base64&gt;$&lt;hash en base64&gt;
    ///
    /// - La sal hace que dos usuarios con la misma contraseña tengan hashes distintos.
    /// - Las muchas iteraciones hacen muy lento probar contraseñas por fuerza bruta.
    /// - Nunca se guarda la contraseña en texto plano.
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

                // Comparación en tiempo constante: no da pistas por lo que tarda en fallar.
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Genera una clave aleatoria legible (para las claves de flota de las máquinas).</summary>
        public static string GenerateKey()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(9);
            string text = Convert.ToBase64String(bytes).Replace('+', 'X').Replace('/', 'Y');
            return "AGRO-" + text;
        }

        /// <summary>Genera un token de sesión aleatorio.</summary>
        public static string GenerateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }
    }
}
