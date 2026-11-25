using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AgroCore.MachineClient.Machine;
using AgroCore.MachineClient.Networking;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.MachineClient
{
    /// <summary>
    /// Programa de la MÁQUINA AUTÓNOMA. Dos modos:
    ///
    ///  1) Una máquina (un proceso = una máquina, como si estuviera instalada en otro equipo):
    ///       AgroCore.MachineClient --id F1-Harvester-01 --key AGRO-DEMO-1 [--server 127.0.0.1] [--port 5000]
    ///
    ///  2) Lanzador de flota: pide al servidor la lista de máquinas de una explotación y arranca
    ///     UN PROCESO INDEPENDIENTE por cada máquina:
    ///       AgroCore.MachineClient --fleet --farm 1 --key AGRO-DEMO-1 [--windows]
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string server = GetArg(args, "--server", NetworkDefaults.Host);
            int port = int.Parse(GetArg(args, "--port", NetworkDefaults.TcpPort.ToString()));
            string key = GetArg(args, "--key", "");

            if (HasFlag(args, "--fleet"))
            {
                int farmId = int.Parse(GetArg(args, "--farm", "1"));
                return RunFleet(server, port, farmId, key, HasFlag(args, "--windows"));
            }

            string machineId = GetArg(args, "--id", "");
            if (machineId == "")
            {
                Console.WriteLine("Uso:");
                Console.WriteLine("  AgroCore.MachineClient --id F1-Harvester-01 --key AGRO-DEMO-1");
                Console.WriteLine("  AgroCore.MachineClient --fleet --farm 1 --key AGRO-DEMO-1 [--windows]");
                return 1;
            }
            return RunMachine(server, port, machineId, key);
        }

        // ==================================================================
        // UNA MÁQUINA
        // ==================================================================

        private static int RunMachine(string server, int port, string machineId, string key)
        {
            Console.Title = "AGROCORE - " + machineId;

            // El tipo se deduce del nombre: F1-Harvester-01 -> Harvester
            string name = machineId.Substring(machineId.IndexOf('-') + 1);
            string typeText = name.Substring(0, name.IndexOf('-'));
            MachineType type;
            if (!Enum.TryParse(typeText, out type))
            {
                Console.WriteLine("Tipo de máquina desconocido: " + typeText);
                return 1;
            }

            MachineProfile profile = ProfileLoader.Load(name, type);
            double initialBattery = 55 + new Random().NextDouble() * 45; // la batería real con la que "arranca"

            ServerLink link = new ServerLink(server, port);
            try
            {
                link.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("No se pudo conectar con el servidor " + server + ":" + port + " - " + ex.Message);
                return 2;
            }

            MachineAuthRequest auth = new MachineAuthRequest();
            auth.MachineId = machineId;
            auth.FleetKey = key;
            auth.MachineType = type;
            auth.Profile = profile;
            auth.InitialBattery = initialBattery;

            MachineAuthResponse response = link.Authenticate(auth);
            if (!response.Success)
            {
                Console.WriteLine("Autenticación rechazada: " + response.Message);
                link.Close();
                return 3;
            }
            Console.WriteLine(machineId + " autenticada: " + response.Message);

            link.Start(response.UdpPort);
            AutonomousMachine machine = new AutonomousMachine(response, type, profile, initialBattery, link);
            machine.Run(); // bucle principal hasta que se cierre la conexión
            link.Close();
            return 0;
        }

        // ==================================================================
        // LANZADOR DE FLOTA
        // ==================================================================

        private static int RunFleet(string server, int port, int farmId, string key, bool ownWindows)
        {
            Console.Title = "AGROCORE - Flota de la explotación " + farmId;

            // 1. Pedir la lista de máquinas al servidor
            FleetQueryResponse fleet;
            try
            {
                Connection connection = Connection.Connect(server, port);
                FleetQueryRequest query = new FleetQueryRequest();
                query.FarmId = farmId;
                query.FleetKey = key;
                connection.Send(query);
                string line = connection.ReadLine();
                connection.Close();
                fleet = MessageSerializer.FromJson<FleetQueryResponse>(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine("No se pudo contactar con el servidor: " + ex.Message);
                return 2;
            }

            if (!fleet.Success)
            {
                Console.WriteLine("Rechazado: " + fleet.Message);
                return 3;
            }

            Console.WriteLine("Explotación: " + fleet.Message + " - " + fleet.Machines.Count + " máquinas");

            // 2. Un proceso por máquina
            string exe = Environment.ProcessPath;
            string prefix = "";
            if (Path.GetFileNameWithoutExtension(exe).ToLower() == "dotnet")
            {
                prefix = "\"" + typeof(Program).Assembly.Location + "\" "; // ejecutado con "dotnet x.dll"
            }

            List<Process> processes = new List<Process>();
            foreach (MachineRegistration machine in fleet.Machines)
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = prefix + "--id " + machine.Id + " --key " + key + " --server " + server + " --port " + port;
                if (ownWindows)
                {
                    info.UseShellExecute = true; // cada máquina en su propia ventana de consola
                }
                else
                {
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                }
                Process process = Process.Start(info);
                processes.Add(process);
                Console.WriteLine("  Arrancada " + machine.Id + " (proceso " + process.Id + ")");
                System.Threading.Thread.Sleep(150);
            }

            Console.WriteLine();
            Console.WriteLine("Flota en marcha. Pulse ENTER para apagar todas las máquinas.");
            string input = null;
            try
            {
                input = Console.ReadLine();
            }
            catch (Exception)
            {
                input = null;
            }
            if (input == null)
            {
                // Sin consola interactiva (lanzado desde un script): seguir hasta que lo cierren
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            }

            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception)
                {
                    // ya terminado
                }
            }
            return 0;
        }

        // ==================================================================

        private static string GetArg(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }

        private static bool HasFlag(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (arg == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
