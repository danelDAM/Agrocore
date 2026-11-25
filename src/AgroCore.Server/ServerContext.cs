using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Server.Security;

namespace AgroCore.Server
{
    /// <summary>
    /// Agrupa todos los servicios del servidor para pasarlos fácilmente a cada cliente.
    /// Se crea una sola vez en Program.cs.
    /// </summary>
    public class ServerContext
    {
        public ConfigStore Config { get; set; }
        public DataStore Store { get; set; }
        public EventLogger Logger { get; set; }
        public AuthService Auth { get; set; }
        public SessionManager Sessions { get; set; }
        public UserManagementService Users { get; set; }
    }
}
