using System;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Una línea del registro de actividad. Se guarda en formato JSON (una por línea)
    /// para poder calcular después las estadísticas contando eventos.
    /// </summary>
    public class LogEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogSeverity Severity { get; set; } = LogSeverity.Info;
        public EventKind Kind { get; set; } = EventKind.Info;
        public int? FarmId { get; set; }
        public string MachineId { get; set; }
        public string Username { get; set; }
        public string FieldId { get; set; }
        public TaskType? TaskType { get; set; }
        public string Message { get; set; } = "";

        public override string ToString()
        {
            string farm = "";
            if (FarmId.HasValue)
            {
                farm = "[F" + FarmId.Value + "] ";
            }
            return "[" + Timestamp.ToString("HH:mm:ss") + "] " + farm + Message;
        }
    }
}
