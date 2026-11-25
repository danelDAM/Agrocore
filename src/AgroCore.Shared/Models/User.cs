using System;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Usuario del sistema. Solo se guarda en el servidor.
    /// Al cliente se le envía un UserDto, que NO lleva el hash de la contraseña.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";

        /// <summary>Hash PBKDF2 con sal. La contraseña nunca se guarda en texto plano.</summary>
        public string PasswordHash { get; set; } = "";

        public string FullName { get; set; } = "";
        public UserRole Role { get; set; }

        /// <summary>Explotación del usuario (0 para el administrador).</summary>
        public int FarmId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }
    }
}
