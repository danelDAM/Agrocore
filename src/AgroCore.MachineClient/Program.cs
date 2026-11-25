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
    /// MAKINA AUTONOMOAREN programa. Bi modu:
    ///
    ///  1) Makina bakarra (prozesu bat = makina bat, beste ordenagailu batean instalatuta egongo balitz bezala):
    ///       AgroCore.MachineClient --id F1-Harvester-01 --key AGRO-DEMO-1 [--server 127.0.0.1] [--port 5000]
    ///
    ///  2) Flota-abiarazlea: ustiategi bateko makinen zerrenda eskatzen dio zerbitzariari eta
    ///     PROZESU INDEPENDENTE BAT abiarazten du makina bakoitzeko:
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
                Console.WriteLine("Erabilera:");
                Console.WriteLine("  AgroCore.MachineClient --id F1-Harvester-01 --key AGRO-DEMO-1");
                Console.WriteLine("  AgroCore.MachineClient --fleet --farm 1 --key AGRO-DEMO-1 [--windows]");
                return 1;
            }
            return RunMachine(server, port, machineId, key);
        }

        // ==================================================================
        // MAKINA BAKARRA
        // ==================================================================

        private static int RunMachine(string server, int port, string machineId, string key)
        {
            Console.Title = "AGROCORE - " + machineId;

            // Mota izenetik ateratzen da: F1-Harvester-01 -> Harvester
            string name = machineId.Substring(machineId.IndexOf('-') + 1);
            string typeText = name.Substring(0, name.IndexOf('-'));
            MachineType type;
            if (!Enum.TryParse(typeText, out type))
            {
                Console.WriteLine("Makina-mota ezezaguna: " + typeText);
                return 1;
            }

            MachineProfile profile = ProfileLoader.Load(name, type);
            double initialBattery = 55 + new Random().NextDouble() * 45; // makinak "abiatzean" duen benetako bateria

            ServerLink link = new ServerLink(server, port);
            try
            {
                link.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ezin izan da " + server + ":" + port + " zerbitzariarekin konektatu - " + ex.Message);
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
                Console.WriteLine("Autentifikazioa ukatuta: " + response.Message);
                link.Close();
                return 3;
            }
            Console.WriteLine(machineId + " autentifikatuta: " + response.Message);

            link.Start(response.UdpPort);
            AutonomousMachine machine = new AutonomousMachine(response, type, profile, initialBattery, link);
            machine.Run(); // begizta nagusia, konexioa itxi arte
            link.Close();
            return 0;
        }

        // ==================================================================
        // FLOTA-ABIARAZLEA
        // ==================================================================

        private static int RunFleet(string server, int port, int farmId, string key, bool ownWindows)
        {
            Console.Title = "AGROCORE - " + farmId + " ustiategiko flota";

            // 1. Makinen zerrenda zerbitzariari eskatu
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
                Console.WriteLine("Ezin izan da zerbitzariarekin harremanetan jarri: " + ex.Message);
                return 2;
            }

            if (!fleet.Success)
            {
                Console.WriteLine("Ukatuta: " + fleet.Message);
                return 3;
            }

            Console.WriteLine("Ustiategia: " + fleet.Message + " - " + fleet.Machines.Count + " makina");

            // 2. Prozesu bat makina bakoitzeko
            string exe = Environment.ProcessPath;
            string prefix = "";
            if (Path.GetFileNameWithoutExtension(exe).ToLower() == "dotnet")
            {
                prefix = "\"" + typeof(Program).Assembly.Location + "\" "; // "dotnet x.dll" bidez exekutatuta
            }

            List<Process> processes = new List<Process>();
            foreach (MachineRegistration machine in fleet.Machines)
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = prefix + "--id " + machine.Id + " --key " + key + " --server " + server + " --port " + port;
                if (ownWindows)
                {
                    info.UseShellExecute = true; // makina bakoitza bere kontsola-leihoan
                }
                else
                {
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                }
                Process process = Process.Start(info);
                processes.Add(process);
                Console.WriteLine("  Abiarazita: " + machine.Id + " (" + process.Id + " prozesua)");
                System.Threading.Thread.Sleep(150);
            }

            Console.WriteLine();
            Console.WriteLine("Flota martxan. Sakatu ENTER makina guztiak itzaltzeko.");
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
                // Kontsola interaktiborik gabe (script batetik abiarazita): jarraitu itxi arte
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
                    // dagoeneko amaituta
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
