using System;
using System.Collections.Generic;
using AgroCore.Server.Machines;
using AgroCore.Server.Security;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Clients
{
    /// <summary>
    /// Atiende a UNA máquina autónoma. Se ejecuta en el hilo propio de esa máquina.
    ///
    /// Cuando la máquina pide trabajo, zona o cargador, ESTE hilo es el que se queda bloqueado
    /// en Monitor.Wait() dentro del coordinador correspondiente: "el servidor mantiene la máquina
    /// esperando". La máquina, mientras tanto, está esperando la respuesta.
    /// </summary>
    public class MachineClientHandler
    {
        private readonly ServerContext context;
        private readonly Connection connection;
        private MachineRuntime machine;
        private FarmRuntime farm;

        public MachineClientHandler(ServerContext context, Connection connection)
        {
            this.context = context;
            this.connection = connection;
        }

        public void Run(string firstLine)
        {
            MachineAuthRequest auth = MessageSerializer.FromJson<MachineAuthRequest>(firstLine);
            if (!Authenticate(auth))
            {
                return;
            }

            try
            {
                while (true)
                {
                    string line = connection.ReadLine();
                    if (line == null)
                    {
                        break; // la máquina se ha desconectado
                    }
                    HandleMessage(MessageSerializer.ReadType(line), line);
                }
            }
            finally
            {
                Disconnect();
            }
        }

        // ==================================================================
        // AUTENTICACIÓN DE LA MÁQUINA
        // ==================================================================

        private bool Authenticate(MachineAuthRequest request)
        {
            MachineAuthResponse response = new MachineAuthResponse();

            MachineRegistration registration = context.Store.GetMachine(request.MachineId);
            Farm farmData = registration != null ? context.Store.GetFarm(registration.FarmId) : null;
            FarmRuntime runtime = registration != null ? context.Farms.Get(registration.FarmId) : null;

            string error = null;
            if (registration == null || farmData == null || runtime == null)
            {
                error = "Máquina no registrada";
            }
            else if (!PasswordHasher.Verify(request.FleetKey, farmData.FleetKeyHash))
            {
                error = "Clave de flota incorrecta";
            }
            else if (registration.Type != request.MachineType)
            {
                error = "El tipo de máquina no coincide con el registrado";
            }
            else if (runtime.Machines[registration.Id].Online)
            {
                error = "Ya hay una máquina conectada con ese identificador";
            }

            if (error != null)
            {
                response.Success = false;
                response.Message = error;
                connection.TrySend(response);
                context.Logger.Log(EventKind.MachineAuthFailed, LogSeverity.Security,
                    "Máquina rechazada '" + request.MachineId + "' desde " + connection.RemoteText + ": " + error,
                    registration != null ? registration.FarmId : (int?)null, request.MachineId);
                return false;
            }

            farm = runtime;
            machine = runtime.Machines[registration.Id];

            string token = PasswordHasher.GenerateToken();
            double battery = Math.Clamp(request.InitialBattery, 0, 100);
            machine.MarkConnected(connection, token, request.Profile, battery, farm.Layout.ParkingPosition(registration.SlotIndex));
            machine.Halted = farm.EmergencyActive; // si hay emergencia, arranca detenida

            response.Success = true;
            response.Message = "Bienvenida a " + farmData.Name;
            response.MachineId = machine.Id;
            response.MachineName = machine.Name;
            response.FarmId = farm.Id;
            response.SessionToken = token;
            response.FieldCount = farm.Layout.FieldCount;
            response.ChargerCount = farm.Layout.ChargerCount;
            response.SlotIndex = registration.SlotIndex;
            response.SlotCount = farm.Layout.MachineSlotCount;
            response.DroneIndex = CountDronesBefore(registration);
            response.UdpPort = context.Config.Current.UdpPort;
            response.TelemetryIntervalMs = context.Config.Current.TelemetryIntervalMs;
            response.Battery = context.Config.Current.Battery;
            connection.Send(response);

            context.Logger.Log(EventKind.MachineAuthenticated, LogSeverity.Success,
                machine.Name + " conectada y autenticada (" + farmData.Name + ") - velocidad " + request.Profile.Speed +
                ", capacidad " + request.Profile.Capacity + ", batería " + battery.ToString("0") + "%",
                farm.Id, machine.Id);

            if (farm.EmergencyActive)
            {
                connection.TrySend(new MachineCommand(MessageTypes.EmergencyStop, machine.Id));
            }
            return true;
        }

        private int CountDronesBefore(MachineRegistration registration)
        {
            int count = 0;
            foreach (MachineRuntime other in farm.GetMachines())
            {
                if (other.Type == MachineType.Sprayer && other.Registration.SlotIndex < registration.SlotIndex)
                {
                    count++;
                }
            }
            return count;
        }

        // ==================================================================
        // MENSAJES DE LA MÁQUINA
        // ==================================================================

        private void HandleMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.TaskRequest:
                    {
                        TaskRequest request = MessageSerializer.FromJson<TaskRequest>(json);
                        // Puede bloquearse aquí (Monitor.Wait) hasta que haya trabajo
                        TaskAssignment assignment = farm.Tasks.WaitForTask(machine, request.MaxWaitMs, () => farm.EmergencyActive);
                        connection.Send(assignment);
                        break;
                    }

                case MessageTypes.TaskStarted:
                    {
                        TaskStarted message = MessageSerializer.FromJson<TaskStarted>(json);
                        farm.Tasks.StartTask(machine, message.TaskId);
                        break;
                    }

                case MessageTypes.TaskCompleted:
                    {
                        TaskCompleted message = MessageSerializer.FromJson<TaskCompleted>(json);
                        farm.Tasks.CompleteTask(machine, message.TaskId);
                        break;
                    }

                case MessageTypes.TaskAborted:
                    {
                        TaskAborted message = MessageSerializer.FromJson<TaskAborted>(json);
                        farm.Tasks.AbortTask(machine, message.TaskId, message.Progress, message.Reason);
                        break;
                    }

                case MessageTypes.ZoneRequest:
                    {
                        ZoneRequest request = MessageSerializer.FromJson<ZoneRequest>(json);
                        ZoneResponse response = new ZoneResponse();
                        response.ZoneId = request.ZoneId;
                        // Puede bloquearse aquí (Monitor.Wait) si la zona está ocupada
                        response.Outcome = farm.Zones.Acquire(machine, request.ZoneId);
                        response.Success = response.Outcome == ZoneOutcome.Granted;
                        connection.Send(response);
                        break;
                    }

                case MessageTypes.ZoneRelease:
                    {
                        ZoneRelease message = MessageSerializer.FromJson<ZoneRelease>(json);
                        farm.Zones.Release(machine, message.ZoneId);
                        break;
                    }

                case MessageTypes.ChargerRequest:
                    {
                        ChargerResponse response = new ChargerResponse();
                        Charger charger;
                        // Puede bloquearse aquí (Monitor.Wait) si todos los cargadores están ocupados
                        response.Outcome = farm.Chargers.Acquire(machine, out charger);
                        response.Success = response.Outcome == ChargerOutcome.Granted;
                        if (charger != null)
                        {
                            response.ChargerId = charger.Id;
                            response.ChargerIndex = charger.Index;
                        }
                        connection.Send(response);
                        break;
                    }

                case MessageTypes.ChargerRelease:
                    {
                        ChargerRelease message = MessageSerializer.FromJson<ChargerRelease>(json);
                        farm.Chargers.Release(machine, message.ChargerId);
                        break;
                    }

                case MessageTypes.MachineEvent:
                    {
                        MachineEventMessage message = MessageSerializer.FromJson<MachineEventMessage>(json);
                        context.Logger.Log(message.Kind, message.Severity, machine.Name + " " + message.Text, farm.Id, machine.Id);
                        break;
                    }

                default:
                    context.Logger.Log(EventKind.Error, LogSeverity.Warning,
                        machine.Name + " envió un mensaje desconocido: " + type, farm.Id, machine.Id);
                    break;
            }
        }

        // ==================================================================
        // DESCONEXIÓN
        // ==================================================================

        private void Disconnect()
        {
            machine.MarkDisconnected();
            farm.Tasks.MachineDisconnected(machine);
            farm.Zones.ReleaseAll(machine);
            farm.Chargers.ReleaseAll(machine);
            farm.WakeAllWaiters();
            context.Logger.Log(EventKind.ClientDisconnected, LogSeverity.Warning,
                machine.Name + " desconectada", farm.Id, machine.Id);
        }

        // ==================================================================
        // LANZADOR DE FLOTA
        // ==================================================================

        /// <summary>Responde a FLEET_QUERY con la lista de máquinas de la explotación (si la clave es correcta).</summary>
        public static void HandleFleetQuery(ServerContext context, Connection connection, string json)
        {
            FleetQueryRequest request = MessageSerializer.FromJson<FleetQueryRequest>(json);
            FleetQueryResponse response = new FleetQueryResponse();
            Farm farm = context.Store.GetFarm(request.FarmId);

            if (farm == null || !PasswordHasher.Verify(request.FleetKey, farm.FleetKeyHash))
            {
                response.Success = false;
                response.Message = "Explotación o clave de flota incorrecta";
                context.Logger.Log(EventKind.MachineAuthFailed, LogSeverity.Security,
                    "FLEET_QUERY rechazado para la explotación " + request.FarmId + " desde " + connection.RemoteText);
            }
            else
            {
                response.Success = true;
                response.Message = farm.Name;
                List<MachineRegistration> machines = context.Store.GetMachines(farm.Id);
                response.Machines = machines;
            }
            connection.Send(response);
        }
    }
}
