using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Messages
{
    /// <summary>WPF -> zerbitzaria. Zerbitzariak egiaztatzen ditu erabiltzailea eta pasahitza (inoiz ez WPFak).</summary>
    public class LoginRequest : NetMessage
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        public LoginRequest()
        {
            Type = MessageTypes.LoginRequest;
        }
    }

    /// <summary>Zerbitzaria -> WPF. Saio-hasierari erantzuna.</summary>
    public class LoginResponse : ResponseMessage
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; }

        /// <summary>Zerbitzarian sortutako saioaren identifikatzailea.</summary>
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

    /// <summary>Zerbitzaria -> WPF: saioa ixten da (erabiltzailea desaktibatuta edo ezabatuta).</summary>
    public class ForceLogoutNotice : NetMessage
    {
        public string Reason { get; set; } = "";

        public ForceLogoutNotice()
        {
            Type = MessageTypes.ForceLogout;
        }
    }
}
