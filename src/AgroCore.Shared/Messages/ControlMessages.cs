namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Orden para una máquina. El mismo mensaje viaja:
    ///   WPF -> servidor   (el servidor comprueba rol y explotación)
    ///   servidor -> máquina (la máquina la ejecuta)
    /// Type puede ser START, STOP, RESUME, RETURN_TO_BASE o EMERGENCY_STOP.
    /// </summary>
    public class MachineCommand : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string IssuedBy { get; set; }

        public MachineCommand()
        {
        }

        public MachineCommand(string type, string machineId)
        {
            Type = type;
            MachineId = machineId;
        }
    }

    /// <summary>
    /// Orden para toda la flota de la explotación:
    /// STOP_ALL, RESUME_ALL, RETURN_ALL_TO_BASE o EMERGENCY_STOP.
    /// FarmId solo lo usa el administrador; para los demás el servidor usa SU explotación.
    /// </summary>
    public class FarmCommand : NetMessage
    {
        public int FarmId { get; set; }

        public FarmCommand()
        {
        }

        public FarmCommand(string type)
        {
            Type = type;
        }
    }
}
