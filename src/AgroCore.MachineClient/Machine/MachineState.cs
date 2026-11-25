using System;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Makinaren egoera fisikoa (posizioa, bateria, egoera...).
    /// Hari nagusiak aldatzen du (makina mugitzen duenak) eta telemetria-hariak irakurtzen du;
    /// horregatik dena lock (stateLock) barruan doa.
    /// </summary>
    public class MachineState
    {
        private readonly object stateLock = new object();
        private readonly Random random = new Random();

        private Position position = new Position();
        private double heading;
        private double speed;
        private double battery;
        private double temperature = 20;
        private double taskProgress;
        private MachineStatus status = MachineStatus.Idle;
        private BatteryStatus batteryStatus = BatteryStatus.BatteryOk;
        private string note;

        public Position GetPosition()
        {
            lock (stateLock)
            {
                return position.Clone();
            }
        }

        public void SetPosition(Position newPosition, double newHeading, double newSpeed)
        {
            lock (stateLock)
            {
                position = newPosition.Clone();
                heading = newHeading;
                speed = newSpeed;
            }
        }

        public void SetSpeed(double newSpeed)
        {
            lock (stateLock)
            {
                speed = newSpeed;
            }
        }

        public double Battery
        {
            get { lock (stateLock) { return battery; } }
            set { lock (stateLock) { battery = Math.Clamp(value, 0, 100); } }
        }

        public MachineStatus Status
        {
            get { lock (stateLock) { return status; } }
            set { lock (stateLock) { status = value; } }
        }

        public BatteryStatus BatteryStatus
        {
            get { lock (stateLock) { return batteryStatus; } }
            set { lock (stateLock) { batteryStatus = value; } }
        }

        public double TaskProgress
        {
            get { lock (stateLock) { return taskProgress; } }
            set { lock (stateLock) { taskProgress = value; } }
        }

        public string Note
        {
            get { lock (stateLock) { return note; } }
            set { lock (stateLock) { note = value; } }
        }

        /// <summary>Egoera eta oharra batera aldatzen ditu.</summary>
        public void Set(MachineStatus newStatus, string newNote)
        {
            lock (stateLock)
            {
                status = newStatus;
                note = newNote;
            }
        }

        /// <summary>Motorraren tenperatura simulatzen du: lanean igo egiten da, geldirik jaitsi.</summary>
        public void UpdateTemperature(bool working, bool moving, double seconds)
        {
            lock (stateLock)
            {
                double target = working ? 78 : (moving ? 62 : 24);
                temperature += (target - temperature) * Math.Min(1, seconds * 0.05);
                temperature += (random.NextDouble() - 0.5) * 0.2;
            }
        }

        /// <summary>Telemetriako UDP datagrama sortzen du uneko egoerarekin.</summary>
        public PositionUpdate ToTelemetry(string machineId, string token)
        {
            lock (stateLock)
            {
                PositionUpdate update = new PositionUpdate();
                update.MachineId = machineId;
                update.SessionToken = token;
                update.X = Math.Round(position.X, 2);
                update.Y = Math.Round(position.Y, 2);
                update.Z = Math.Round(position.Z, 2);
                update.Heading = Math.Round(heading, 1);
                update.Speed = Math.Round(speed, 2);
                update.Battery = Math.Round(battery, 2);
                update.Temperature = Math.Round(temperature, 1);
                update.Status = status;
                update.BatteryStatus = batteryStatus;
                update.TaskProgress = Math.Round(taskProgress, 1);
                update.Note = note;
                return update;
            }
        }
    }
}
