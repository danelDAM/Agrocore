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
    /// Recibe por UDP la telemetría (TELEMETRY_BATCH) que el servidor envía cada 200 ms.
    ///
    /// UDP porque son datos que se repiten continuamente: si se pierde un paquete no pasa nada,
    /// el siguiente llega enseguida. Lo importante (órdenes, login...) va siempre por TCP.
    /// </summary>
    public class TelemetryReceiver
    {
        private UdpClient udp;
        private Thread thread;
        private volatile bool running;

        /// <summary>Puerto local en el que escuchamos (se lo decimos al servidor con UDP_SUBSCRIBE).</summary>
        public int Port { get; private set; }

        /// <summary>Llega un lote de telemetría (se lanza desde el hilo UDP).</summary>
        public event Action<TelemetryBatch> BatchReceived;

        public void Start()
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)); // puerto 0 = el sistema elige uno libre
            Port = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
            running = true;

            thread = new Thread(ReceiveLoop);
            thread.IsBackground = true;
            thread.Name = "Receptor UDP";
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
                    // socket cerrado al salir o paquete incorrecto: se ignora
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
