using System;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Machines
{
    /// <summary>
    /// Lo que el servidor sabe en cada momento de una máquina: estado, posición, conexión,
    /// órdenes pendientes y estadísticas acumuladas.
    ///
    /// La leen y escriben varios hilos (el de la conexión TCP de la máquina, el receptor UDP,
    /// el que envía el estado al WPF...). Por eso los datos de estado se tocan dentro de lock(stateLock).
    /// Los indicadores de control (Halted, ReturnRequested, Disconnected) son "volatile" para que
    /// todos los hilos vean siempre el último valor.
    /// </summary>
    public class MachineRuntime
    {
        private readonly object stateLock = new object();

        public MachineRuntime(MachineRegistration registration, Position parking)
        {
            Registration = registration;
            position = parking.Clone();
            status = MachineStatus.Offline;
        }

        public MachineRegistration Registration { get; private set; }

        public string Id
        {
            get { return Registration.Id; }
        }

        public int FarmId
        {
            get { return Registration.FarmId; }
        }

        public string Name
        {
            get { return Registration.Name; }
        }

        public MachineType Type
        {
            get { return Registration.Type; }
        }

        // ---------------- Conexión ----------------
        public Connection Connection { get; set; }
        public string SessionToken { get; set; }
        public MachineProfile Profile { get; set; } = new MachineProfile();
        public bool Online { get; set; }

        // ---------------- Control (los cambian las órdenes del operador) ----------------
        /// <summary>STOP o EMERGENCY_STOP: la máquina está detenida.</summary>
        public volatile bool Halted;

        /// <summary>Se ha ordenado RETURN_TO_BASE.</summary>
        public volatile bool ReturnRequested;

        /// <summary>La máquina se ha desconectado.</summary>
        public volatile bool Disconnected;

        /// <summary>
        /// ¿Hay que cancelar la espera en la que está el hilo de esta máquina?
        /// Lo comprueban los bucles con Monitor.Wait() cada vez que se despiertan.
        /// </summary>
        public bool ShouldAbortWait(bool alsoWhenReturning)
        {
            if (Disconnected || Halted)
            {
                return true;
            }
            return alsoWhenReturning && ReturnRequested;
        }

        // ---------------- Estado (protegido con stateLock) ----------------
        private Position position;
        private double heading;
        private double speed;
        private double battery;
        private double temperature;
        private double taskProgress;
        private MachineStatus status;
        private BatteryStatus batteryStatus;
        private string note;        // nota que envía la máquina por telemetría
        private string serverNote;  // nota que pone el servidor mientras la máquina espera (más detallada)
        private string currentTaskId;
        private TaskType? currentTaskType;
        private string currentFieldId;
        private DateTime lastTelemetry = DateTime.MinValue;

        // ---------------- Estadísticas acumuladas ----------------
        private double distance;
        private double activeSeconds;
        private double workingSeconds;
        private double movingSeconds;
        private double waitingSeconds;
        private double chargingSeconds;
        private double batteryConsumed;
        private int tasksCompleted;
        private int tasksAborted;
        private int chargeCycles;

        public double Battery
        {
            get { lock (stateLock) { return battery; } }
        }

        public MachineStatus CurrentStatus
        {
            get { lock (stateLock) { return status; } }
        }

        public Position GetPosition()
        {
            lock (stateLock)
            {
                return position.Clone();
            }
        }

        public void SetNote(string text)
        {
            lock (stateLock)
            {
                serverNote = text;
            }
        }

        /// <summary>Se llama al autenticarse la máquina.</summary>
        public void MarkConnected(Connection connection, string token, MachineProfile profile, double initialBattery, Position start)
        {
            lock (stateLock)
            {
                Connection = connection;
                SessionToken = token;
                Profile = profile;
                Online = true;
                Disconnected = false;
                ReturnRequested = false;
                battery = initialBattery;
                position = start.Clone();
                status = MachineStatus.Idle;
                note = null;
                lastTelemetry = DateTime.Now;
            }
        }

        public void MarkDisconnected()
        {
            lock (stateLock)
            {
                Online = false;
                Disconnected = true;
                status = MachineStatus.Offline;
                currentTaskId = null;
                currentTaskType = null;
                currentFieldId = null;
                note = "desconectada";
            }
        }

        public void SetCurrentTask(string taskId, TaskType? type, string fieldId)
        {
            lock (stateLock)
            {
                currentTaskId = taskId;
                currentTaskType = type;
                currentFieldId = fieldId;
            }
        }

        public string CurrentTaskId
        {
            get { lock (stateLock) { return currentTaskId; } }
        }

        public double TaskProgress
        {
            get { lock (stateLock) { return taskProgress; } }
        }

        public void CountTaskCompleted()
        {
            lock (stateLock) { tasksCompleted++; }
        }

        public void CountTaskAborted()
        {
            lock (stateLock) { tasksAborted++; }
        }

        public void CountChargeCycle()
        {
            lock (stateLock) { chargeCycles++; }
        }

        /// <summary>Actualiza el estado con un datagrama UDP de telemetría y acumula estadísticas.</summary>
        public void ApplyTelemetry(PositionUpdate update)
        {
            lock (stateLock)
            {
                DateTime now = DateTime.Now;
                double seconds = (now - lastTelemetry).TotalSeconds;
                if (seconds > 2 || seconds < 0)
                {
                    seconds = 0; // primer dato o hueco largo: no contar
                }
                lastTelemetry = now;

                Position newPosition = new Position(update.X, update.Y, update.Z);
                distance += position.DistanceTo(newPosition);
                if (update.Battery < battery)
                {
                    batteryConsumed += battery - update.Battery;
                }

                // Tiempo según el estado
                if (status != MachineStatus.Offline && status != MachineStatus.Parked)
                {
                    activeSeconds += seconds;
                }
                if (status == MachineStatus.Working) workingSeconds += seconds;
                if (status == MachineStatus.MovingToField || status == MachineStatus.ReturningToBase) movingSeconds += seconds;
                if (status == MachineStatus.WaitingForTask || status == MachineStatus.WaitingForZone
                    || status == MachineStatus.WaitingForCharger || status == MachineStatus.Yielding) waitingSeconds += seconds;
                if (status == MachineStatus.Charging) chargingSeconds += seconds;

                position = newPosition;
                heading = update.Heading;
                speed = update.Speed;
                battery = update.Battery;
                temperature = update.Temperature;
                taskProgress = update.TaskProgress;
                status = update.Status;
                batteryStatus = update.BatteryStatus;
                note = update.Note;
            }
        }

        /// <summary>Copia del estado para enviarla al centro de control.</summary>
        public Machine ToDto()
        {
            lock (stateLock)
            {
                Machine dto = new Machine();
                dto.Id = Id;
                dto.FarmId = FarmId;
                dto.Name = Name;
                dto.Type = Type;
                dto.Position = position.Clone();
                dto.Heading = heading;
                dto.Speed = speed;
                dto.Battery = battery;
                dto.Status = Online ? status : MachineStatus.Offline;
                dto.BatteryStatus = batteryStatus;
                dto.CurrentTaskId = currentTaskId;
                dto.CurrentTaskType = currentTaskType;
                dto.CurrentFieldId = currentFieldId;
                dto.TaskProgress = taskProgress;
                dto.Temperature = temperature;
                dto.Online = Online;
                bool waiting = status == MachineStatus.WaitingForTask || status == MachineStatus.WaitingForZone
                               || status == MachineStatus.WaitingForCharger;
                dto.Note = (waiting && serverNote != null) ? serverNote : note;
                dto.Profile = Profile;
                return dto;
            }
        }

        public Telemetry ToTelemetry()
        {
            lock (stateLock)
            {
                Telemetry t = new Telemetry();
                t.MachineId = Id;
                t.X = position.X;
                t.Y = position.Y;
                t.Z = position.Z;
                t.Heading = heading;
                t.Battery = battery;
                t.Speed = speed;
                t.Temperature = temperature;
                t.Status = Online ? status : MachineStatus.Offline;
                t.BatteryStatus = batteryStatus;
                t.TaskProgress = taskProgress;
                return t;
            }
        }

        public MachineStatistics GetStatistics()
        {
            lock (stateLock)
            {
                MachineStatistics s = new MachineStatistics();
                s.MachineId = Id;
                s.Name = Name;
                s.Type = Type;
                s.DistanceKm = distance / 1000.0 * 10; // 1 unidad del mapa = 10 m
                s.ActiveMinutes = activeSeconds / 60.0;
                s.WorkingMinutes = workingSeconds / 60.0;
                s.MovingMinutes = movingSeconds / 60.0;
                s.WaitingMinutes = waitingSeconds / 60.0;
                s.ChargingMinutes = chargingSeconds / 60.0;
                s.BatteryConsumed = batteryConsumed;
                s.TasksCompleted = tasksCompleted;
                s.TasksAborted = tasksAborted;
                s.ChargeCycles = chargeCycles;
                if (activeSeconds > 0)
                {
                    s.Efficiency = Math.Min(100, (workingSeconds + movingSeconds) / activeSeconds * 100);
                }
                return s;
            }
        }

        /// <summary>Recupera las estadísticas guardadas en disco al arrancar el servidor.</summary>
        public void LoadStatistics(MachineStatistics s)
        {
            if (s == null)
            {
                return;
            }
            lock (stateLock)
            {
                distance = s.DistanceKm * 1000.0 / 10;
                activeSeconds = s.ActiveMinutes * 60;
                workingSeconds = s.WorkingMinutes * 60;
                movingSeconds = s.MovingMinutes * 60;
                waitingSeconds = s.WaitingMinutes * 60;
                chargingSeconds = s.ChargingMinutes * 60;
                batteryConsumed = s.BatteryConsumed;
                tasksCompleted = s.TasksCompleted;
                tasksAborted = s.TasksAborted;
                chargeCycles = s.ChargeCycles;
            }
        }
    }
}
