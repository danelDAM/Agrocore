namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Makina baterako agindua. Mezu bera bidaiatzen du:
    ///   WPF -> zerbitzaria   (zerbitzariak rola eta ustiategia egiaztatzen ditu)
    ///   zerbitzaria -> makina (makinak exekutatzen du)
    /// Type izan daiteke START, STOP, RESUME, RETURN_TO_BASE edo EMERGENCY_STOP.
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
    /// Ustiategiko flota osorako agindua:
    /// STOP_ALL, RESUME_ALL, RETURN_ALL_TO_BASE edo EMERGENCY_STOP.
    /// FarmId administratzaileak bakarrik erabiltzen du; besteentzat zerbitzariak BERE ustiategia erabiltzen du.
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
