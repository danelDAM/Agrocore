using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>
    /// Zerbitzariak 200 ms-ro bidaltzen duen telemetria (TELEMETRY_BATCH) UDP bidez jasotzen du.
    ///
    /// UDP, etengabe errepikatzen diren datuak direlako: pakete bat galtzen bada ez da ezer gertatzen,
    /// hurrengoa berehala iristen da. Garrantzitsua dena (aginduak, login...) beti TCP bidez doa.
    /// </summary>
    public class TelemetryReceiver
    {
        private UdpClient udp;
        private Thread thread;
        private volatile bool running;

        /// <summary>Entzuten dugun ataka lokala (zerbitzariari UDP_SUBSCRIBE bidez esaten diogu).</summary>
        public int Port { get; private set; }

        /// <summary>Telemetria-sorta bat iristen da (UDP haritik jaurtitzen da).</summary>
        public event Action<TelemetryBatch> BatchReceived;

        public void Start()
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)); // 0 ataka = sistemak libre bat aukeratzen du
            Port = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
            running = true;

            thread = new Thread(ReceiveLoop);
            thread.IsBackground = true;
            thread.Name = "UDP hartzailea";
            thread.Start();
        }

        private void ReceiveLoop()
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = udp.Receive(ref sender);
                    string json = Encoding.UTF8.GetString(data);
                    if (MessageSerializer.ReadType(json) == MessageTypes.TelemetryBatch)
                    {
                        TelemetryBatch batch = MessageSerializer.FromJson<TelemetryBatch>(json);
                        if (BatchReceived != null)
                        {
                            BatchReceived(batch);
                        }
                    }
                }
                catch (Exception)
                {
                    // irtetean itxitako socketa edo pakete okerra: ez ikusi egiten zaio
                }
            }
        }

        public void Stop()
        {
            running = false;
            if (udp != null)
            {
                udp.Close();
            }
        }
    }
}
