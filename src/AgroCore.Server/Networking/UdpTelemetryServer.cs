using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AgroCore.Server.Machines;
using AgroCore.Server.Security;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Networking
{
    /// <summary>
    /// Telemetría por UDP (rápida, sin conexión; si se pierde un paquete llega el siguiente).
    ///
    /// Dos hilos:
    ///  - Receptor: recibe POSITION_UPDATE de las máquinas y actualiza su estado.
    ///    Comprueba el token de sesión para descartar datagramas falsos.
    ///  - Emisor: cada 200 ms envía a cada centro de control un TELEMETRY_BATCH SOLO con las
    ///    máquinas de la explotación que ese usuario tiene permiso para ver.
    /// </summary>
    public class UdpTelemetryServer
    {
        private readonly ServerContext context;
        private readonly int port;
        private UdpClient udp;
        private volatile bool running;

        public UdpTelemetryServer(ServerContext context, int port)
        {
            this.context = context;
            this.port = port;
        }

        public void Start()
        {
            udp = new UdpClient(port);
            // En Windows, si un cliente UDP se cierra, Receive lanzaría errores "conexión restablecida".
            // Con esta opción se ignoran (SIO_UDP_CONNRESET = -1744830452).
            try
            {
                udp.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            }
            catch (Exception)
            {
                // en otros sistemas no existe esta opción
            }
            running = true;

            Thread receiver = new Thread(ReceiveLoop);
            receiver.IsBackground = true;
            receiver.Name = "UDP-Receive";
            receiver.Start();

            Thread sender = new Thread(SendLoop);
            sender.IsBackground = true;
            sender.Name = "UDP-Send";
            sender.Start();

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success, "Servidor UDP de telemetría en el puerto " + port);
        }

        public void Stop()
        {
            running = false;
            udp.Close();
        }

        private void ReceiveLoop()
        {
            while (running)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref remote); // bloqueante
                    string json = Encoding.UTF8.GetString(data);

                    if (MessageSerializer.ReadType(json) != MessageTypes.PositionUpdate)
                    {
                        continue;
                    }

                    PositionUpdate update = MessageSerializer.FromJson<PositionUpdate>(json);
                    MachineRuntime machine = context.Farms.FindMachine(update.MachineId);

                    // Seguridad: solo se acepta si el token coincide con la sesión de esa máquina
                    if (machine == null || !machine.Online || machine.SessionToken != update.SessionToken)
                    {
                        continue;
                    }
                    machine.ApplyTelemetry(update);
                }
                catch (SocketException)
                {
                    if (!running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    // datagrama mal formado: se ignora
                }
            }
        }

        private void SendLoop()
        {
            while (running)
            {
                Thread.Sleep(context.Config.Current.TelemetryIntervalMs);

                // Se prepara un lote por explotación (solo una vez aunque la vean varios usuarios)
                Dictionary<int, byte[]> batches = new Dictionary<int, byte[]>();

                foreach (UserSession session in context.Sessions.GetAll())
                {
                    if (session.UdpEndPoint == null)
                    {
                        continue;
                    }

                    byte[] data;
                    if (!batches.TryGetValue(session.ViewFarmId, out data))
                    {
                        FarmRuntime farm = context.Farms.Get(session.ViewFarmId);
                        if (farm == null)
                        {
                            continue;
                        }
                        TelemetryBatch batch = new TelemetryBatch();
                        batch.FarmId = farm.Id;
                        foreach (MachineRuntime machine in farm.GetMachines())
                        {
                            batch.Items.Add(machine.ToTelemetry());
                        }
                        data = Encoding.UTF8.GetBytes(MessageSerializer.ToJson(batch));
                        batches[session.ViewFarmId] = data;
                    }

                    try
                    {
                        udp.Send(data, data.Length, session.UdpEndPoint);
                    }
                    catch (Exception)
                    {
                        // si falla un envío UDP no pasa nada
                    }
                }
            }
        }
    }
}
