using System;
using System.IO;
using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Fields;
using AgroCore.Server.Machines;
using AgroCore.Server.Statistics;
using AgroCore.Server.Synchronization;
using AgroCore.Server.Logging;
using AgroCore.Server.Networking;
using AgroCore.Server.Security;
using AgroCore.Shared.Enums;

namespace AgroCore.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "AGROCORE SERVER";
            Console.WriteLine("==============================================");
            Console.WriteLine("   AGROCORE ZERBITZARIA - Nekazaritza-koordinazioa");
            Console.WriteLine("==============================================");

            // data/ eta logs/ karpetak exekutagarriaren ondoan sortzen dira (edo --data bidez adierazitakoan)
            string baseFolder = AppContext.BaseDirectory;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--data")
                {
                    baseFolder = args[i + 1];
                }
            }

            // 1. Zerbitzuak sortu
            ServerContext context = new ServerContext();
            context.Config = new ConfigStore(Path.Combine(baseFolder, "data", "server-config.json"));
            context.Logger = new EventLogger(Path.Combine(baseFolder, "logs"));
            context.Logger.Start();
            context.Store = new DataStore(Path.Combine(baseFolder, "data", "agrocore-data.json"));
            context.Store.Load();
            context.Sessions = new SessionManager();
            context.Auth = new AuthService(context.Store, context.Config, context.Logger);
            context.Users = new UserManagementService(context.Store, context.Sessions, context.Logger);

            context.Farms = new FarmManager(context.Store, context.Config, context.Logger);
            context.Farms.LoadAll();
            context.Commands = new MachineCommandService(context.Logger);
            context.Statistics = new StatisticsService(context.Logger, context.Farms);
            context.StateBuilder = new FarmStateBuilder(context.Logger, () => context.Config.Current);

            // Administratzaileak ustiategi bat sortzen edo ezabatzen badu, martxan jartzen edo gelditzen da
            context.Users.FarmCreated += farm => context.Farms.AddFarm(farm);
            context.Users.FarmDeleted += farmId => context.Farms.RemoveFarm(farmId);

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success,
                "AGROCORE zerbitzaria abian, " + context.Farms.GetAll().Count + " ustiategirekin");

            // 2. Zerbitzariaren hariak abiarazi
            TcpServer tcpServer = new TcpServer(context, context.Config.Current.TcpPort);
            UdpTelemetryServer udpServer = new UdpTelemetryServer(context, context.Config.Current.UdpPort);
            try
            {
                tcpServer.Start();
                udpServer.Start();
            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.WriteLine();
                Console.WriteLine("ERROREA: " + context.Config.Current.TcpPort + "/" + context.Config.Current.UdpPort
                                  + " atakak erabiltzen ari dira jada. Beste AGROCORE zerbitzari bat martxan al dago?");
                context.Logger.Stop();
                Environment.ExitCode = 1;
                return;
            }

            GrowthService growth = new GrowthService(context.Farms);
            growth.Start();

            StateBroadcaster broadcaster = new StateBroadcaster(context);
            broadcaster.Start();

            // 3. Kontsola: zerbitzariaren administratzailearentzako komando errazak
            Console.WriteLine("Komandoak: 'status' = egoera ikusi, 'debug' = guneen erreserbak erakutsi/ezkutatu, 'exit' = irten");
            while (true)
            {
                string command = Console.ReadLine();
                if (command == null)
                {
                    // Kontsolarik gabe (atzeko planoan exekutatuta): betiko itxaron
                    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                }
                command = command.Trim().ToLower();

                if (command == "exit" || command == "quit")
                {
                    break;
                }
                if (command == "status")
                {
                    Console.WriteLine("Saio irekiak: " + context.Sessions.Count);
                }
                if (command == "debug")
                {
                    context.Logger.ShowDebugInConsole = !context.Logger.ShowDebugInConsole;
                    Console.WriteLine("Arazketa-mezuak: " + (context.Logger.ShowDebugInConsole ? "BAI" : "EZ"));
                }
            }

            // 4. Itzaltze ordenatua
            context.Logger.Log(EventKind.ServerStopped, LogSeverity.Info, "Zerbitzaria geldituta");
            broadcaster.Stop();
            growth.Stop();
            udpServer.Stop();
            tcpServer.Stop();
            context.Farms.SaveAll();
            context.Store.Save();
            context.Logger.Stop();
        }
    }
}
