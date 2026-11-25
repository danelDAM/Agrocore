using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Messages
{
    /// <summary>WPF -> servidor. El servidor comprueba usuario y contraseña (nunca el WPF).</summary>
    public class LoginRequest : NetMessage
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        public LoginRequest()
        {
            Type = MessageTypes.LoginRequest;
        }
    }

    /// <summary>Servidor -> WPF. Respuesta al login.</summary>
    public class LoginResponse : ResponseMessage
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; }

        /// <summary>Identificador de la sesión creada en el servidor.</summary>
        public string SessionToken { get; set; }

        public LoginResponse()
        {
            Type = MessageTypes.LoginResponse;
        }
    }

    public class LogoutRequest : NetMessage
    {
        public LogoutRequest()
        {
            Type = MessageTypes.LogoutRequest;
        }
    }

    public class ChangePasswordRequest : NetMessage
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";

        public ChangePasswordRequest()
        {
            Type = MessageTypes.ChangePasswordRequest;
        }
    }

    /// <summary>Servidor -> WPF: se cierra la sesión (usuario desactivado o eliminado).</summary>
    public class ForceLogoutNotice : NetMessage
    {
        public string Reason { get; set; } = "";

        public ForceLogoutNotice()
        {
            Type = MessageTypes.ForceLogout;
        }
    }
}
