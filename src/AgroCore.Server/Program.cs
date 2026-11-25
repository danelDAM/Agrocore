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
            Console.WriteLine("   AGROCORE SERVER - Coordinación agrícola");
            Console.WriteLine("==============================================");

            // Las carpetas data/ y logs/ se crean junto al ejecutable (o en la indicada con --data)
            string baseFolder = AppContext.BaseDirectory;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--data")
                {
                    baseFolder = args[i + 1];
                }
            }

            // 1. Crear los servicios
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

            // Si el administrador crea o borra una explotación, se pone en marcha o se detiene
            context.Users.FarmCreated += farm => context.Farms.AddFarm(farm);
            context.Users.FarmDeleted += farmId => context.Farms.RemoveFarm(farmId);

            context.Logger.Log(EventKind.ServerStarted, LogSeverity.Success,
                "Servidor AGROCORE iniciado con " + context.Farms.GetAll().Count + " explotaciones");

            // 2. Arrancar los hilos del servidor
            TcpServer tcpServer = new TcpServer(context, context.Config.Current.TcpPort);
            tcpServer.Start();

            UdpTelemetryServer udpServer = new UdpTelemetryServer(context, context.Config.Current.UdpPort);
            udpServer.Start();

            GrowthService growth = new GrowthService(context.Farms);
            growth.Start();

            StateBroadcaster broadcaster = new StateBroadcaster(context);
            broadcaster.Start();

            // 3. Consola: comandos sencillos para el administrador del servidor
            Console.WriteLine("Comandos: 'status' = ver estado, 'debug' = ver/ocultar reservas de zonas, 'exit' = salir");
            while (true)
            {
                string command = Console.ReadLine();
                if (command == null)
                {
                    // Sin consola (ejecutado en segundo plano): esperar para siempre
                    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                }
                command = command.Trim().ToLower();

                if (command == "exit" || command == "quit")
                {
                    break;
                }
                if (command == "status")
                {
                    Console.WriteLine("Sesiones abiertas: " + context.Sessions.Count);
                }
                if (command == "debug")
                {
                    context.Logger.ShowDebugInConsole = !context.Logger.ShowDebugInConsole;
                    Console.WriteLine("Mensajes de depuración: " + (context.Logger.ShowDebugInConsole ? "SÍ" : "NO"));
                }
            }

            // 4. Apagado ordenado
            context.Logger.Log(EventKind.ServerStopped, LogSeverity.Info, "Servidor detenido");
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
