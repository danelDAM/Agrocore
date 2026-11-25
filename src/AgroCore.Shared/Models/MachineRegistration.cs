using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Alta de una máquina en una explotación (se guarda en el servidor).</summary>
    public class MachineRegistration
    {
        /// <summary>Identificador único en todo el sistema, por ejemplo "F1-Harvester-01".</summary>
        public string Id { get; set; } = "";

        public int FarmId { get; set; }

        /// <summary>Nombre que se ve en la explotación, por ejemplo "Harvester-01".</summary>
        public string Name { get; set; } = "";

        public MachineType Type { get; set; }

        /// <summary>Número de plaza de aparcamiento en la base.</summary>
        public int SlotIndex { get; set; }
    }
}
