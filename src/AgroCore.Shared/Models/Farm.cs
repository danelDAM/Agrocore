using System;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Nekazaritza-ustiategia. Soroek, makinek, kargagailuek, zereginek eta erregistroek FarmId bat dute;
    /// horrela zerbitzariak beti egiazta dezake gauza bakoitza zein ustiategirena den.
    /// </summary>
    public class Farm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int OwnerId { get; set; }
        public int FieldCount { get; set; } = 6;
        public int ChargerCount { get; set; } = 3;

        /// <summary>Ustiategi honetako makinek autentifikatzeko erabiltzen duten gakoaren hash-a.</summary>
        public string FleetKeyHash { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
