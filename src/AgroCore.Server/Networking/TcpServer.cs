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
    /// Bezero anitzeko TCP zerbitzaria.
    ///
    /// Klaseko adibide klasikoaren berdina:
    ///   - Hari batek konexioak onartzen ditu (AcceptTcpClient blokeatzailea da).
    ///   - Bezero bakoitzeko Thread berri bat sortzen da hura artatzeko.
    ///
    /// Bezero batek bidaltzen duen LEHEN mezuak zer den adierazten du:
    ///   LOGIN_REQUEST  -> WPF Kontrol Zentro bat da
    ///   MACHINE_AUTH   -> makina autonomo bat da
    ///   FLEET_QUERY    -> flota-abiarazlea da (makinen zerrenda eskatu eta deskonektatu egiten da)
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

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success, "TCP zerbitzaria entzuten, ataka: " + port);
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
                    socket = listener.AcceptTcpClient(); // blokeatzailea: norbait konektatu arte itxaroten du
                }
                catch (SocketException)
                {
                    break; // listener-a gelditu egin da
                }

                Thread clientThread = new Thread(() => HandleClient(socket));
                clientThread.IsBackground = true;
                clientThread.Name = "Client";
                clientThread.Start();
            }
        }

        /// <summary>Bezero bakoitzaren hari propioan exekutatzen da.</summary>
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

                    case MessageTypes.MachineAuth:
                        MachineClientHandler machine = new MachineClientHandler(context, connection);
                        machine.Run(firstLine);
                        break;

                    case MessageTypes.FleetQuery:
                        MachineClientHandler.HandleFleetQuery(context, connection, firstLine);
                        break;

                    default:
                        connection.TrySend(OperationResult.Fail(type, "Lehen mezuak LOGIN_REQUEST, MACHINE_AUTH edo FLEET_QUERY izan behar du"));
                        context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                            "Konexioa baztertuta (" + connection.RemoteText + "): lehen mezua '" + type + "'");
                        break;
                }
            }
            catch (Exception ex)
            {
                context.Logger.Log(EventKind.Error, LogSeverity.Error,
                    "Errorea bezeroarekin (" + connection.RemoteText + "): " + ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
    }
}
