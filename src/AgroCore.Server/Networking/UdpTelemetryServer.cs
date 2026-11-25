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
    /// UDP bidezko telemetria (azkarra, konexiorik gabea; pakete bat galtzen bada, hurrengoa iristen da).
    ///
    /// Bi hari:
    ///  - Hartzailea: makinen POSITION_UPDATE mezuak jaso eta haien egoera eguneratzen du.
    ///    Saioaren tokena egiaztatzen du datagrama faltsuak baztertzeko.
    ///  - Igorlea: 200 ms-ro, Kontrol Zentro bakoitzari TELEMETRY_BATCH bat bidaltzen dio, erabiltzaile
    ///    horrek ikusteko baimena duen ustiategiko makinekin BAKARRIK.
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
            // Windows-en, UDP bezero bat ixten bada, Receive-k "konexioa berrezarri da" erroreak jaurtiko lituzke.
            // Aukera honekin ez ikusi egiten zaie (SIO_UDP_CONNRESET = -1744830452).
            try
            {
                udp.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            }
            catch (Exception)
            {
                // beste sistemetan ez dago aukera hau
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

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success, "UDP telemetria-zerbitzaria, ataka: " + port);
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
                    byte[] data = udp.Receive(ref remote); // blokeatzailea
                    string json = Encoding.UTF8.GetString(data);

                    if (MessageSerializer.ReadType(json) != MessageTypes.PositionUpdate)
                    {
                        continue;
                    }

                    PositionUpdate update = MessageSerializer.FromJson<PositionUpdate>(json);
                    MachineRuntime machine = context.Farms.FindMachine(update.MachineId);

                    // Segurtasuna: tokena makina horren saioarekin bat badator bakarrik onartzen da
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
                    // gaizki osatutako datagrama: ez ikusi egiten zaio
                }
            }
        }

        private void SendLoop()
        {
            while (running)
            {
                Thread.Sleep(context.Config.Current.TelemetryIntervalMs);

                // Sorta bat prestatzen da ustiategi bakoitzeko (behin bakarrik, hainbat erabiltzailek ikusi arren)
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
                        // UDP bidalketa batek huts egiten badu, ez da ezer gertatzen
                    }
                }
            }
        }
    }
}
