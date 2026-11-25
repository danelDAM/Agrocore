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

    /// <summary>Aviso que se muestra en el panel de alertas.</summary>
    public class Alert
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AlertLevel Level { get; set; }
        public string Text { get; set; } = "";
    }
}
