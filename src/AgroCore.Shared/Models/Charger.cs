namespace AgroCore.Shared.Models
{
    /// <summary>Estación de carga de la base.</summary>
    public class Charger
    {
        /// <summary>CHARGER-01, CHARGER-02...</summary>
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public int Index { get; set; }
        public Position Position { get; set; } = new Position();

        /// <summary>Máquina que está cargando (null si está libre).</summary>
        public string OccupiedBy { get; set; }

        /// <summary>Máquinas esperando cargador (solo para mostrarlo).</summary>
        public int QueueLength { get; set; }

        public Charger Clone()
        {
            Charger copy = new Charger();
            copy.Id = Id;
            copy.FarmId = FarmId;
            copy.Index = Index;
            copy.Position = Position.Clone();
            copy.OccupiedBy = OccupiedBy;
            copy.QueueLength = QueueLength;
            return copy;
        }
    }
}
