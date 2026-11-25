using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Logging
{
    /// <summary>
    /// Registro de actividad del servidor.
    ///
    /// Patrón PRODUCTOR / CONSUMIDOR:
    ///  - Cualquier hilo llama a Log(...) (productor). Solo añade la entrada a una cola, que es rápido.
    ///  - Un único hilo escritor (consumidor) saca las entradas de la cola y las escribe en disco.
    /// Así los hilos que atienden máquinas nunca se quedan esperando a que se escriba un archivo.
    ///
    /// Se escriben dos archivos en la carpeta logs/:
    ///  - agrocore-AAAAMMDD.log : texto legible  "[10:32:15] Seeder-01 completed TASK-103"
    ///  - events.jsonl          : una entrada JSON por línea. Al arrancar se vuelve a leer para
    ///                            calcular las estadísticas (los logs sirven para las estadísticas).
    /// </summary>
    public class EventLogger
    {
        private const int MaxEntriesInMemory = 50000;

        private readonly string folder;
        private readonly BlockingCollection<LogEntry> writeQueue = new BlockingCollection<LogEntry>();
        private readonly List<LogEntry> entries = new List<LogEntry>();
        private readonly object entriesLock = new object();
        private readonly object consoleLock = new object();
        private long nextId = 1;
        private Thread writerThread;

        public EventLogger(string folder)
        {
            this.folder = folder;
            Directory.CreateDirectory(folder);
        }

        /// <summary>Si es false no se muestran en consola los mensajes de nivel Debug (reservas de zonas).</summary>
        public bool ShowDebugInConsole { get; set; } = false;

        public void Start()
        {
            LoadPreviousEvents();
            writerThread = new Thread(WriterLoop);
            writerThread.IsBackground = true;
            writerThread.Name = "LogWriter";
            writerThread.Start();
        }

        public void Stop()
        {
            writeQueue.CompleteAdding();
            if (writerThread != null)
            {
                writerThread.Join(2000);
            }
        }

        // ------------------------------------------------------------------
        // Métodos para registrar
        // ------------------------------------------------------------------

        public void Log(EventKind kind, LogSeverity severity, string message)
        {
            Log(kind, severity, message, null, null, null, null, null);
        }

        public void Log(EventKind kind, LogSeverity severity, string message, int? farmId, string machineId)
        {
            Log(kind, severity, message, farmId, machineId, null, null, null);
        }

        public void Log(EventKind kind, LogSeverity severity, string message, int? farmId, string machineId,
                        string username, string fieldId, TaskType? taskType)
        {
            LogEntry entry = new LogEntry();
            entry.Timestamp = DateTime.Now;
            entry.Kind = kind;
            entry.Severity = severity;
            entry.Message = message;
            entry.FarmId = farmId;
            entry.MachineId = machineId;
            entry.Username = username;
            entry.FieldId = fieldId;
            entry.TaskType = taskType;

            lock (entriesLock)
            {
                entry.Id = nextId;
                nextId++;
                entries.Add(entry);
                if (entries.Count > MaxEntriesInMemory)
                {
                    entries.RemoveRange(0, 5000);
                }
            }

            WriteToConsole(entry);

            if (!writeQueue.IsAddingCompleted)
            {
                writeQueue.Add(entry); // el hilo escritor lo guardará en disco
            }
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        /// <summary>Últimas entradas de una explotación (farmId 0 = todas).</summary>
        public List<LogEntry> GetRecent(int farmId, int count, bool includeDebug)
        {
            List<LogEntry> result = new List<LogEntry>();
            lock (entriesLock)
            {
                for (int i = entries.Count - 1; i >= 0 && result.Count < count; i--)
                {
                    LogEntry entry = entries[i];
                    if (!includeDebug && entry.Severity == LogSeverity.Debug)
                    {
                        continue;
                    }
                    if (farmId == 0 || entry.FarmId == farmId)
                    {
                        result.Add(entry);
                    }
                }
            }
            return result; // de más nueva a más antigua
        }

        /// <summary>Entradas de una explotación en un periodo (para estadísticas e informes).</summary>
        public List<LogEntry> GetEntries(int farmId, DateTime from, DateTime to)
        {
            List<LogEntry> result = new List<LogEntry>();
            lock (entriesLock)
            {
                foreach (LogEntry entry in entries)
                {
                    if (entry.FarmId == farmId && entry.Timestamp >= from && entry.Timestamp <= to)
                    {
                        result.Add(entry);
                    }
                }
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Escritura
        // ------------------------------------------------------------------

        private void WriteToConsole(LogEntry entry)
        {
            if (entry.Severity == LogSeverity.Debug && !ShowDebugInConsole)
            {
                return;
            }

            lock (consoleLock)
            {
                ConsoleColor color = ConsoleColor.Gray;
                if (entry.Severity == LogSeverity.Success) color = ConsoleColor.Green;
                if (entry.Severity == LogSeverity.Warning) color = ConsoleColor.Yellow;
                if (entry.Severity == LogSeverity.Error) color = ConsoleColor.Red;
                if (entry.Severity == LogSeverity.Security) color = ConsoleColor.Magenta;

                Console.ForegroundColor = color;
                Console.WriteLine(entry.ToString());
                Console.ResetColor();
            }
        }

        /// <summary>Hilo consumidor: escribe en disco todo lo que llega a la cola.</summary>
        private void WriterLoop()
        {
            string eventsPath = Path.Combine(folder, "events.jsonl");

            // GetConsumingEnumerable se bloquea (sin gastar CPU) mientras la cola está vacía.
            foreach (LogEntry entry in writeQueue.GetConsumingEnumerable())
            {
                try
                {
                    string textPath = Path.Combine(folder, "agrocore-" + entry.Timestamp.ToString("yyyyMMdd") + ".log");
                    string level = entry.Severity.ToString().ToUpper();
                    File.AppendAllText(textPath, entry.ToString() + "  (" + level + ")" + Environment.NewLine);

                    if (entry.Severity != LogSeverity.Debug)
                    {
                        File.AppendAllText(eventsPath, MessageSerializer.ToJson(entry) + "\n");
                    }
                }
                catch (IOException)
                {
                    // si el archivo está bloqueado un momento se pierde esa línea, pero el servidor sigue
                }
            }
        }

        /// <summary>Al arrancar, recupera los eventos anteriores para que las estadísticas sean acumuladas.</summary>
        private void LoadPreviousEvents()
        {
            string eventsPath = Path.Combine(folder, "events.jsonl");
            if (!File.Exists(eventsPath))
            {
                return;
            }

            int loaded = 0;
            foreach (string line in File.ReadLines(eventsPath))
            {
                try
                {
                    LogEntry entry = JsonSerializer.Deserialize<LogEntry>(line, MessageSerializer.Options);
                    if (entry != null)
                    {
                        lock (entriesLock)
                        {
                            entry.Id = nextId;
                            nextId++;
                            entries.Add(entry);
                        }
                        loaded++;
                    }
                }
                catch (JsonException)
                {
                    // línea dañada: se ignora
                }
            }

            lock (entriesLock)
            {
                if (entries.Count > MaxEntriesInMemory)
                {
                    entries.RemoveRange(0, entries.Count - MaxEntriesInMemory);
                }
            }
            Console.WriteLine("Eventos anteriores cargados: " + loaded);
        }
    }
}
