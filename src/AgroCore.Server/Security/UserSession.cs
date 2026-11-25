using System;
using System.Net;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// WPF Kontrol Zentro batetik konektatutako erabiltzaile baten saioa.
    /// Zerbitzariak sortzen du saioa ondo hasi ondoren, eta saioa ixtean edo deskonektatzean ezabatzen du.
    /// </summary>
    public class UserSession
    {
        public string Token { get; set; } = "";
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public UserRole Role { get; set; }

        /// <summary>Erabiltzailearen ustiategia (administratzailea ez bada, ikus dezakeen bakarra).</summary>
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";

        /// <summary>
        /// Orain ikusten ari den ustiategia. Nekazari/operadore/behatzailearentzat BETI bere FarmId da.
        /// Administratzaileak SELECT_FARM bidez alda dezake.
        /// </summary>
        public int ViewFarmId { get; set; }

        /// <summary>Kontrol Zentroaren TCP konexioa (egoera eta alertak bidaltzeko).</summary>
        public Connection Connection { get; set; }

        /// <summary>Telemetria UDP bidez nora bidali (null, UDP_SUBSCRIBE bidali arte).</summary>
        public IPEndPoint UdpEndPoint { get; set; }

        public DateTime LoginTime { get; set; } = DateTime.Now;

        public bool IsAdmin
        {
            get { return Role == UserRole.Admin; }
        }
    }
}
