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
    /// Makinaren eta zerbitzariaren arteko komunikazioa.
    ///
    /// TCP: IRAKURLE-hari batek zerbitzariak bidaltzen duen guztia jasotzen du.
    ///   - Hari nagusia zain duen erantzuna bada (TASK_ASSIGNMENT, ZONE_RESPONSE,
    ///     CHARGER_RESPONSE), "postontzi" batean uzten du eta Monitor.PulseAll egiten du hura esnatzeko.
    ///   - Agindu bat bada (STOP, RESUME, RETURN_TO_BASE, EMERGENCY_STOP, TASK_CANCELLED),
    ///     CommandReceived gertaera deitzen du makinak berehala erreakziona dezan.
    ///
    /// UDP: SendTelemetry-k posizioa, bateria eta abar bidaltzen ditu segundoko hainbat aldiz.
    /// </summary>
    public class ServerLink
    {
        private readonly string host;
        private readonly int tcpPort;
        private Connection connection;
        private UdpClient udp;
        private IPEndPoint udpServer;

        // Hari nagusiak zain duen erantzunerako postontzia
        private readonly object mailboxLock = new object();
        private string expectedType;
        private string mailbox;
        private volatile bool closed;

        /// <summary>Zerbitzariaren agindu bat iristen denean deitzen da (irakurle-haritik).</summary>
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

        /// <summary>Autentifikazioa (irakurle-haria abiarazi aurretik egiten da: bidali eta erantzuna irakurri).</summary>
        public MachineAuthResponse Authenticate(MachineAuthRequest request)
        {
            connection.Send(request);
            string line = connection.ReadLine();
            if (line == null)
            {
                throw new Exception("Zerbitzariak konexioa itxi du");
            }
            return MessageSerializer.FromJson<MachineAuthResponse>(line);
        }

        /// <summary>UDPa prestatzen du eta TCPko irakurle-haria abiarazten du.</summary>
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
                        Monitor.PulseAll(mailboxLock); // erantzun honen zain dagoen haria esnatu
                    }
                }

                if (!isAwaitedReply && CommandReceived != null)
                {
                    CommandReceived(type, line);
                }
            }

            // Konexioa galduta: zain dagoena esnatu, blokeatuta gera ez dadin
            closed = true;
            lock (mailboxLock)
            {
                Monitor.PulseAll(mailboxLock);
            }
        }

        /// <summary>
        /// Eskaera bat bidaltzen du eta adierazitako motako erantzunaren zain geratzen da (Monitor.Wait).
        /// Itxarotea luzea izan daiteke: zerbitzariak ez du erantzuten zeregina, gunea edo kargagailua egon arte.
        /// null itzultzen du konexioa galtzen bada.
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

        /// <summary>Erantzunik behar ez duen abisu bat bidaltzen du (TASK_STARTED, ZONE_RELEASE...).</summary>
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
                // UDP: bidalketa batek huts egiten badu, ez da ezer gertatzen
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
