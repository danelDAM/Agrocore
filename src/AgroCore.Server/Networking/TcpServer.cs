using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AgroCore.Server.Clients;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Networking
{
    /// <summary>
    /// Servidor TCP multicliente.
    ///
    /// Igual que el ejemplo clásico de clase:
    ///   - Un hilo acepta conexiones (AcceptTcpClient es bloqueante).
    ///   - Por cada cliente se crea un Thread nuevo que lo atiende.
    ///
    /// El PRIMER mensaje que envía un cliente dice qué es:
    ///   LOGIN_REQUEST  -> es un centro de control WPF
    ///   MACHINE_AUTH   -> es una máquina autónoma
    ///   FLEET_QUERY    -> es el lanzador de flota (pide la lista de máquinas y se desconecta)
    /// </summary>
    public class TcpServer
    {
        private readonly ServerContext context;
        private readonly int port;
        private TcpListener listener;
        private bool running;

        public TcpServer(ServerContext context, int port)
        {
            this.context = context;
            this.port = port;
        }

        public void Start()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(50);
            running = true;

            Thread acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "TCP-Accept";
            acceptThread.Start();

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success, "Servidor TCP escuchando en el puerto " + port);
        }

        public void Stop()
        {
            running = false;
            listener.Stop();
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient socket;
                try
                {
                    socket = listener.AcceptTcpClient(); // bloqueante: espera a que alguien se conecte
                }
                catch (SocketException)
                {
                    break; // el listener se ha parado
                }

                Thread clientThread = new Thread(() => HandleClient(socket));
                clientThread.IsBackground = true;
                clientThread.Name = "Client";
                clientThread.Start();
            }
        }

        /// <summary>Se ejecuta en el hilo propio de cada cliente.</summary>
        private void HandleClient(TcpClient socket)
        {
            Connection connection = new Connection(socket);
            try
            {
                string firstLine = connection.ReadLine();
                if (firstLine == null)
                {
                    return;
                }

                string type = MessageSerializer.ReadType(firstLine);
                switch (type)
                {
                    case MessageTypes.LoginRequest:
                        ControlCenterClient controlCenter = new ControlCenterClient(context, connection);
                        controlCenter.Run(firstLine);
                        break;

                    default:
                        connection.TrySend(OperationResult.Fail(type, "El primer mensaje debe ser LOGIN_REQUEST, MACHINE_AUTH o FLEET_QUERY"));
                        context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                            "Conexión rechazada desde " + connection.RemoteText + ": primer mensaje '" + type + "'");
                        break;
                }
            }
            catch (Exception ex)
            {
                context.Logger.Log(EventKind.Error, LogSeverity.Error,
                    "Error con el cliente " + connection.RemoteText + ": " + ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
    }
}
