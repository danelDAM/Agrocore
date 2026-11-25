using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.MachineClient.Networking
{
    /// <summary>
    /// Comunicación de la máquina con el servidor.
    ///
    /// TCP: un hilo LECTOR recibe todo lo que manda el servidor.
    ///   - Si es la respuesta que el hilo principal está esperando (TASK_ASSIGNMENT, ZONE_RESPONSE,
    ///     CHARGER_RESPONSE), la deja en un "buzón" y hace Monitor.PulseAll para despertarlo.
    ///   - Si es una orden (STOP, RESUME, RETURN_TO_BASE, EMERGENCY_STOP, TASK_CANCELLED)
    ///     llama al evento CommandReceived para que la máquina reaccione en el momento.
    ///
    /// UDP: SendTelemetry envía la posición, batería, etc. varias veces por segundo.
    /// </summary>
    public class ServerLink
    {
        private readonly string host;
        private readonly int tcpPort;
        private Connection connection;
        private UdpClient udp;
        private IPEndPoint udpServer;

        // Buzón para la respuesta que espera el hilo principal
        private readonly object mailboxLock = new object();
        private string expectedType;
        private string mailbox;
        private volatile bool closed;

        /// <summary>Se llama (desde el hilo lector) cuando llega una orden del servidor.</summary>
        public event Action<string, string> CommandReceived;

        public ServerLink(string host, int tcpPort)
        {
            this.host = host;
            this.tcpPort = tcpPort;
        }

        public bool IsConnected
        {
            get { return !closed; }
        }

        public void Connect()
        {
            connection = Connection.Connect(host, tcpPort);
        }

        /// <summary>Autenticación (se hace antes de arrancar el hilo lector: enviar y leer la respuesta).</summary>
        public MachineAuthResponse Authenticate(MachineAuthRequest request)
        {
            connection.Send(request);
            string line = connection.ReadLine();
            if (line == null)
            {
                throw new Exception("El servidor cerró la conexión");
            }
            return MessageSerializer.FromJson<MachineAuthResponse>(line);
        }

        /// <summary>Prepara el UDP y arranca el hilo lector de TCP.</summary>
        public void Start(int udpPort)
        {
            udp = new UdpClient();
            IPAddress address;
            if (!IPAddress.TryParse(host, out address))
            {
                address = Dns.GetHostAddresses(host)[0];
            }
            udpServer = new IPEndPoint(address, udpPort);

            Thread reader = new Thread(ReadLoop);
            reader.IsBackground = true;
            reader.Name = "ServerReader";
            reader.Start();
        }

        private void ReadLoop()
        {
            while (true)
            {
                string line = connection.ReadLine();
                if (line == null)
                {
                    break;
                }

                string type = MessageSerializer.ReadType(line);
                bool isAwaitedReply = false;

                lock (mailboxLock)
                {
                    if (type == expectedType)
                    {
                        mailbox = line;
                        isAwaitedReply = true;
                        Monitor.PulseAll(mailboxLock); // despertar al hilo que espera esta respuesta
                    }
                }

                if (!isAwaitedReply && CommandReceived != null)
                {
                    CommandReceived(type, line);
                }
            }

            // Conexión perdida: despertar a quien esté esperando para que no se quede bloqueado
            closed = true;
            lock (mailboxLock)
            {
                Monitor.PulseAll(mailboxLock);
            }
        }

        /// <summary>
        /// Envía una petición y espera (Monitor.Wait) la respuesta del tipo indicado.
        /// La espera puede ser larga: el servidor no responde hasta que hay tarea, zona o cargador.
        /// Devuelve null si se pierde la conexión.
        /// </summary>
        public T Request<T>(NetMessage request, string responseType)
        {
            lock (mailboxLock)
            {
                expectedType = responseType;
                mailbox = null;
            }

            connection.Send(request);

            lock (mailboxLock)
            {
                while (mailbox == null && !closed)
                {
                    Monitor.Wait(mailboxLock, 1000);
                }
                string line = mailbox;
                mailbox = null;
                expectedType = null;
                if (line == null)
                {
                    return default(T);
                }
                return MessageSerializer.FromJson<T>(line);
            }
        }

        /// <summary>Envía un aviso que no necesita respuesta (TASK_STARTED, ZONE_RELEASE...).</summary>
        public void Send(NetMessage message)
        {
            connection.TrySend(message);
        }

        public void SendTelemetry(PositionUpdate update)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(MessageSerializer.ToJson(update));
                udp.Send(data, data.Length, udpServer);
            }
            catch (Exception)
            {
                // UDP: si falla un envío no pasa nada
            }
        }

        public void Close()
        {
            closed = true;
            if (connection != null)
            {
                connection.Close();
            }
            if (udp != null)
            {
                udp.Close();
            }
        }
    }
}
