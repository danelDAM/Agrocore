using AgroCore.Server.Logging;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;

namespace AgroCore.Server.Machines
{
    /// <summary>
    /// Ejecuta las órdenes del operador sobre las máquinas.
    ///
    ///   WPF -> TCP -> SERVIDOR (valida) -> aquí -> MÁQUINA (ejecuta) -> telemetría -> SERVIDOR -> WPF
    ///
    /// El servidor no solo reenvía la orden: también actualiza su propio estado (Halted, ReturnRequested)
    /// y despierta a los hilos que estén esperando (Monitor.PulseAll) para que cancelen su espera.
    /// Quien llama a estos métodos YA ha comprobado el rol y que la máquina sea de su explotación.
    /// </summary>
    public class MachineCommandService
    {
        private readonly EventLogger logger;

        public MachineCommandService(EventLogger logger)
        {
            this.logger = logger;
        }

        /// <summary>Orden a una sola máquina. Devuelve null si va bien o el texto del error.</summary>
        public string Execute(FarmRuntime farm, MachineRuntime machine, string command, string username)
        {
            if (!machine.Online)
            {
                return machine.Name + " no está conectada";
            }

            switch (command)
            {
                case MessageTypes.Stop:
                    machine.Halted = true;
                    farm.WakeAllWaiters(); // si estaba esperando algo, que deje de esperar
                    Forward(machine, command, username);
                    logger.Log(EventKind.MachineStopped, LogSeverity.Warning,
                        username + " detuvo " + machine.Name, farm.Id, machine.Id, username, null, null);
                    return null;

                case MessageTypes.Resume:
                case MessageTypes.Start:
                    if (farm.EmergencyActive)
                    {
                        return "Parada de emergencia activa: use REANUDAR TODO para desbloquear la explotación";
                    }
                    machine.Halted = false;
                    machine.ReturnRequested = false;
                    Forward(machine, command, username);
                    logger.Log(command == MessageTypes.Start ? EventKind.MachineStarted : EventKind.MachineResumed, LogSeverity.Info,
                        username + " " + (command == MessageTypes.Start ? "arrancó " : "reanudó ") + machine.Name,
                        farm.Id, machine.Id, username, null, null);
                    return null;

                case MessageTypes.ReturnToBase:
                    machine.ReturnRequested = true;
                    farm.WakeAllWaiters();
                    Forward(machine, command, username);
                    logger.Log(EventKind.ReturnToBase, LogSeverity.Info,
                        username + " mandó " + machine.Name + " a la base", farm.Id, machine.Id, username, null, null);
                    return null;

                default:
                    return "Orden desconocida: " + command;
            }
        }

        /// <summary>Orden global sobre toda la flota de la explotación.</summary>
        public string ExecuteForFarm(FarmRuntime farm, string command, string username)
        {
            int count = 0;
            switch (command)
            {
                case MessageTypes.EmergencyStop:
                    farm.EmergencyActive = true;
                    foreach (MachineRuntime machine in farm.GetMachines())
                    {
                        machine.Halted = true;
                        if (machine.Online)
                        {
                            Forward(machine, MessageTypes.EmergencyStop, username);
                            count++;
                        }
                    }
                    farm.WakeAllWaiters();
                    logger.Log(EventKind.EmergencyStop, LogSeverity.Error,
                        "PARADA DE EMERGENCIA ordenada por " + username + " (" + count + " máquinas detenidas)",
                        farm.Id, null, username, null, null);
                    return null;

                case MessageTypes.StopAll:
                    foreach (MachineRuntime machine in farm.GetMachines())
                    {
                        if (machine.Online)
                        {
                            machine.Halted = true;
                            Forward(machine, MessageTypes.Stop, username);
                            count++;
                        }
                    }
                    farm.WakeAllWaiters();
                    logger.Log(EventKind.MachineStopped, LogSeverity.Warning,
                        username + " detuvo toda la flota (" + count + " máquinas)", farm.Id, null, username, null, null);
                    return null;

                case MessageTypes.ResumeAll:
                    bool wasEmergency = farm.EmergencyActive;
                    farm.EmergencyActive = false;
                    foreach (MachineRuntime machine in farm.GetMachines())
                    {
                        machine.Halted = false;
                        machine.ReturnRequested = false;
                        if (machine.Online)
                        {
                            Forward(machine, MessageTypes.Resume, username);
                            count++;
                        }
                    }
                    logger.Log(EventKind.MachineResumed, LogSeverity.Info,
                        username + " reanudó toda la flota (" + count + " máquinas)" + (wasEmergency ? " - fin de la emergencia" : ""),
                        farm.Id, null, username, null, null);
                    return null;

                case MessageTypes.ReturnAllToBase:
                    foreach (MachineRuntime machine in farm.GetMachines())
                    {
                        if (machine.Online)
                        {
                            machine.ReturnRequested = true;
                            Forward(machine, MessageTypes.ReturnToBase, username);
                            count++;
                        }
                    }
                    farm.WakeAllWaiters();
                    logger.Log(EventKind.ReturnToBase, LogSeverity.Info,
                        username + " mandó toda la flota a la base (" + count + " máquinas)", farm.Id, null, username, null, null);
                    return null;

                default:
                    return "Orden desconocida: " + command;
            }
        }

        /// <summary>Envía la orden a la máquina por su conexión TCP.</summary>
        private void Forward(MachineRuntime machine, string command, string username)
        {
            MachineCommand message = new MachineCommand(command, machine.Id);
            message.IssuedBy = username;
            if (machine.Connection != null)
            {
                machine.Connection.TrySend(message);
            }
        }
    }
}
