using System;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Explotación agrícola. Campos, máquinas, cargadores, tareas y logs llevan un FarmId,
    /// así el servidor puede comprobar siempre a qué explotación pertenece cada cosa.
    /// </summary>
    public class Farm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int OwnerId { get; set; }
        public int FieldCount { get; set; } = 6;
        public int ChargerCount { get; set; } = 3;

        /// <summary>Hash de la clave con la que se autentican las máquinas de esta explotación.</summary>
        public string FleetKeyHash { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
