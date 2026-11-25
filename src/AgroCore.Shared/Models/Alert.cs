using System;

namespace AgroCore.Shared.Models
{
    public enum AlertLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>Alerten panelean erakusten den abisua.</summary>
    public class Alert
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AlertLevel Level { get; set; }
        public string Text { get; set; } = "";
    }
}
