using System;
using System.Net;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Sesión de un usuario conectado desde un centro de control WPF.
    /// La crea el servidor tras un login correcto y la borra al cerrar sesión o desconectarse.
    /// </summary>
    public class UserSession
    {
        public string Token { get; set; } = "";
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public UserRole Role { get; set; }

        /// <summary>Explotación del usuario (la única que puede ver si no es administrador).</summary>
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";

        /// <summary>
        /// Explotación que está viendo ahora. Para agricultor/operador/observador es SIEMPRE su FarmId.
        /// El administrador puede cambiarla con SELECT_FARM.
        /// </summary>
        public int ViewFarmId { get; set; }

        /// <summary>Conexión TCP del centro de control (para enviarle el estado y las alertas).</summary>
        public Connection Connection { get; set; }

        /// <summary>Dónde enviarle la telemetría por UDP (null hasta que manda UDP_SUBSCRIBE).</summary>
        public IPEndPoint UdpEndPoint { get; set; }

        public DateTime LoginTime { get; set; } = DateTime.Now;

        public bool IsAdmin
        {
            get { return Role == UserRole.Admin; }
        }
    }
}
