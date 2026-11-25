using System.Collections.Generic;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Data
{
    /// <summary>
    /// Zerbitzariak diskoan gordetzen duen guztia (data/agrocore-data.json).
    /// Tamaina honetako proiektu baterako, JSON fitxategi bat nahikoa da eta erraz berrikusten da.
    /// </summary>
    public class DatabaseFile
    {
        public int NextUserId { get; set; } = 1;
        public int NextFarmId { get; set; } = 1;
        public List<User> Users { get; set; } = new List<User>();
        public List<Farm> Farms { get; set; } = new List<Farm>();
        public List<MachineRegistration> Machines { get; set; } = new List<MachineRegistration>();
        public List<Field> Fields { get; set; } = new List<Field>();

        /// <summary>Makina bakoitzaren estatistika metatuak (gakoa = makinaren id-a).</summary>
        public Dictionary<string, MachineStatistics> MachineStats { get; set; } = new Dictionary<string, MachineStatistics>();
    }
}
