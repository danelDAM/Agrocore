using System;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Sistemaren erabiltzailea. Zerbitzarian soilik gordetzen da.
    /// Bezeroari UserDto bat bidaltzen zaio, eta horrek EZ darama pasahitzaren hash-a.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";

        /// <summary>PBKDF2 hash-a gatzarekin. Pasahitza ez da inoiz testu arruntean gordetzen.</summary>
        public string PasswordHash { get; set; } = "";

        public string FullName { get; set; } = "";
        public UserRole Role { get; set; }

        /// <summary>Erabiltzailearen ustiategia (0 administratzailearentzat).</summary>
        public int FarmId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }
    }
}
