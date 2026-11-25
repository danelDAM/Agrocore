namespace AgroCore.Server.Clients
{
    /// <summary>
    /// Parte del ControlCenterClient con los mensajes de la explotación
    /// (estado, máquinas, órdenes, planes de cultivo, estadísticas, informes).
    /// </summary>
    public partial class ControlCenterClient
    {
        /// <summary>Devuelve true si el mensaje era de esta parte y se ha atendido.</summary>
        private bool HandleFarmMessage(string type, string json)
        {
            return false; // se completa en el Sprint 2
        }
    }
}
