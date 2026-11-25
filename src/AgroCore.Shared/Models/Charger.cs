namespace AgroCore.Shared.Models
{
    /// <summary>Basearen karga-estazioa.</summary>
    public class Charger
    {
        /// <summary>CHARGER-01, CHARGER-02...</summary>
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public int Index { get; set; }
        public Position Position { get; set; } = new Position();

        /// <summary>Kargatzen ari den makina (null libre badago).</summary>
        public string OccupiedBy { get; set; }

        /// <summary>Kargagailuaren zain dauden makinak (erakusteko soilik).</summary>
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
