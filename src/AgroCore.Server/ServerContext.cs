using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Server.Security;
using AgroCore.Server.Statistics;
using AgroCore.Server.Synchronization;

namespace AgroCore.Server
{
    /// <summary>
    /// Zerbitzariaren zerbitzu guztiak biltzen ditu, bezero bakoitzari erraz pasatzeko.
    /// Behin bakarrik sortzen da, Program.cs fitxategian.
    /// </summary>
    public class ServerContext
    {
        public ConfigStore Config { get; set; }
        public DataStore Store { get; set; }
        public EventLogger Logger { get; set; }
        public AuthService Auth { get; set; }
        public SessionManager Sessions { get; set; }
        public UserManagementService Users { get; set; }

        // 2. sprinta: simulazioa eta koordinazioa
        public FarmManager Farms { get; set; }
        public MachineCommandService Commands { get; set; }
        public StatisticsService Statistics { get; set; }
        public FarmStateBuilder StateBuilder { get; set; }
    }
}
