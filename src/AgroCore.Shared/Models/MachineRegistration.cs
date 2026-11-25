using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Makina bat ustiategi batean erregistratzea (zerbitzarian gordetzen da).</summary>
    public class MachineRegistration
    {
        /// <summary>Sistema osoan identifikatzaile bakarra, adibidez "F1-Harvester-01".</summary>
        public string Id { get; set; } = "";

        public int FarmId { get; set; }

        /// <summary>Ustiategian ikusten den izena, adibidez "Harvester-01".</summary>
        public string Name { get; set; } = "";

        public MachineType Type { get; set; }

        /// <summary>Basean duen aparkalekuaren zenbakia.</summary>
        public int SlotIndex { get; set; }
    }
}
