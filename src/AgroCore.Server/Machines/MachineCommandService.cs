using AgroCore.Server.Logging;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;

namespace AgroCore.Server.Machines
{
    /// <summary>
    /// Operadorearen aginduak exekutatzen ditu makinetan.
    ///
    ///   WPF -> TCP -> ZERBITZARIA (balidatu) -> hemen -> MAKINA (exekutatu) -> telemetria -> ZERBITZARIA -> WPF
    ///
    /// Zerbitzariak ez du agindua birbidali soilik: bere egoera ere eguneratzen du (Halted, ReturnRequested)
    /// eta zain dauden hariak esnatzen ditu (Monitor.PulseAll), itxaronaldia bertan behera utz dezaten.
    /// Metodo hauei deitzen dienak JADA egiaztatu ditu rola eta makina bere ustiategikoa dela.
    /// </summary>
    public class MachineCommandService
    {
        private readonly EventLogger logger;

        public MachineCommandService(EventLogger logger)
        {
            this.logger = logger;
        }

        /// <summary>Makina bakar baterako agindua. null itzultzen du ondo badoa, edo errorearen testua.</summary>
        public string Execute(FarmRuntime farm, MachineRuntime machine, string command, string username)
        {
            if (!machine.Online)
            {
                return machine.Name + " ez dago konektatuta";
            }

            switch (command)
            {
                case MessageTypes.Stop:
                    machine.Halted = true;
                    farm.WakeAllWaiters(); // zerbaiten zain bazegoen, itxaroteari utz diezaiola
                    Forward(machine, command, username);
                    logger.Log(EventKind.MachineStopped, LogSeverity.Warning,
                        username + " erabiltzaileak " + machine.Name + " gelditu du", farm.Id, machine.Id, username, null, null);
                    return null;

                case MessageTypes.Resume:
                case MessageTypes.Start:
                    if (farm.EmergencyActive)
                    {
                        return "Larrialdiko geldialdia aktibo dago: erabili GUZTIAK BERREKIN ustiategia desblokeatzeko";
                    }
                    machine.Halted = false;
                    machine.ReturnRequested = false;
                    Forward(machine, command, username);
                    logger.Log(command == MessageTypes.Start ? EventKind.MachineStarted : EventKind.MachineResumed, LogSeverity.Info,
                        username + " erabiltzaileak " + machine.Name + (command == MessageTypes.Start ? " abiarazi du" : " berrekin du"),
                        farm.Id, machine.Id, username, null, null);
                    return null;

                case MessageTypes.ReturnToBase:
                    machine.ReturnRequested = true;
                    farm.WakeAllWaiters();
                    Forward(machine, command, username);
                    logger.Log(EventKind.ReturnToBase, LogSeverity.Info,
                        username + " erabiltzaileak " + machine.Name + " basera bidali du", farm.Id, machine.Id, username, null, null);
                    return null;

                default:
                    return "Agindu ezezaguna: " + command;
            }
        }

        /// <summary>Ustiategiko flota osoarentzako agindu orokorra.</summary>
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
                        "LARRIALDIKO GELDIALDIA, " + username + " erabiltzaileak aginduta (" + count + " makina geldituta)",
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
                        username + " erabiltzaileak flota osoa gelditu du (" + count + " makina)", farm.Id, null, username, null, null);
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
                        username + " erabiltzaileak flota osoa berrekin du (" + count + " makina)" + (wasEmergency ? " - larrialdiaren amaiera" : ""),
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
                        username + " erabiltzaileak flota osoa basera bidali du (" + count + " makina)", farm.Id, null, username, null, null);
                    return null;

                default:
                    return "Agindu ezezaguna: " + command;
            }
        }

        /// <summary>Agindua makinari bidaltzen dio bere TCP konexioaren bidez.</summary>
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
