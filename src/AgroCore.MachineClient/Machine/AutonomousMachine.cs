using System;
using System.Threading;
using AgroCore.MachineClient.Battery;
using AgroCore.MachineClient.Networking;
using AgroCore.MachineClient.Tasks;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Makina autonomoaren "garuna". Bi hari ditu:
    ///  - Hari nagusia (Run): begizta batean erabakitzen du zer egin (zeregina eskatu, lan egin, kargatzera joan...).
    ///  - Telemetria-haria: 200 ms-ro UDP bidez posizioa, bateria, egoera... bidaltzen ditu.
    /// Gainera, ServerLink-en irakurle-hariak zerbitzariaren aginduak jasotzen ditu (STOP, RESUME...).
    /// </summary>
    public class AutonomousMachine
    {
        private readonly string machineId;
        private readonly string name;
        private readonly MachineType type;
        private readonly string token;
        private readonly int telemetryIntervalMs;
        private readonly ServerLink link;
        private readonly FarmLayout layout;
        private readonly Position parkingSpot;

        private readonly MachineState state = new MachineState();
        private readonly MachineControl control = new MachineControl();
        private readonly BatteryManager battery;
        private readonly Navigator navigator;
        private readonly TaskExecutor executor;
        private readonly object consoleLock = new object();

        public AutonomousMachine(MachineAuthResponse auth, MachineType type, MachineProfile profile, double initialBattery, ServerLink link)
        {
            machineId = auth.MachineId;
            name = auth.MachineName;
            this.type = type;
            token = auth.SessionToken;
            telemetryIntervalMs = Math.Max(50, auth.TelemetryIntervalMs);
            this.link = link;

            layout = new FarmLayout(auth.FieldCount, auth.ChargerCount, auth.SlotCount);
            parkingSpot = layout.ParkingPosition(auth.SlotIndex);

            state.Battery = initialBattery;
            battery = new BatteryManager(state, profile, auth.Battery, layout, type);
            navigator = new Navigator(machineId, type, profile, state, control, battery, link, layout, auth.DroneIndex);
            executor = new TaskExecutor(machineId, type, profile, state, control, battery, navigator, link, layout, Log);
            navigator.PlaceAtSlot(parkingSpot);

            link.CommandReceived += OnCommand;
        }

        // ==================================================================
        // ZERBITZARIAREN AGINDUAK (sareko irakurle-harian exekutatzen da)
        // ==================================================================

        private void OnCommand(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.Stop:
                    Log("STOP agindua jasota");
                    control.Stop(false);
                    break;
                case MessageTypes.EmergencyStop:
                    Log("LARRIALDIKO GELDIALDIA!");
                    control.Stop(true);
                    break;
                case MessageTypes.Resume:
                case MessageTypes.Start:
                    Log(type + " agindua jasota");
                    control.Resume();
                    break;
                case MessageTypes.ReturnToBase:
                    Log("RETURN_TO_BASE agindua jasota");
                    control.RequestReturn();
                    break;
                case MessageTypes.TaskCancelled:
                    TaskCancelledNotice notice = MessageSerializer.FromJson<TaskCancelledNotice>(json);
                    Log(notice.TaskId + " zeregina zerbitzariak bertan behera utzi du");
                    control.CancelTask(notice.TaskId);
                    break;
            }
        }

        // ==================================================================
        // BEGIZTA NAGUSIA
        // ==================================================================

        public void Run()
        {
            Thread telemetry = new Thread(TelemetryLoop);
            telemetry.IsBackground = true;
            telemetry.Name = "Telemetry";
            telemetry.Start();

            Log("Martxan. Bateria: %" + state.Battery.ToString("0"));

            try
            {
                while (link.IsConnected)
                {
                    navigator.PauseIfHalted();

                    // Basera itzultzeko agindu didate?
                    if (control.TakeReturnRequest())
                    {
                        ReturnToBaseAndPark();
                        continue;
                    }

                    // Bateria baxua?
                    if (battery.IsLow())
                    {
                        GoCharge("bateria baxua (%" + state.Battery.ToString("0") + ")");
                        continue;
                    }

                    // Basean ezer egin gabe eta bateria gutxirekin badago, kargatzeko aprobetxatzen du
                    if (navigator.IsAtBase && state.Battery < 60)
                    {
                        GoCharge("prebentziozko karga (%" + state.Battery.ToString("0") + ")");
                        continue;
                    }

                    // Zerbitzariari lana eskatu. Basean behar adina itxaroten du;
                    // basetik kanpo gutxi itxaroten du eta, ezer ez badago, aparkatzera itzultzen da.
                    int maxWait = navigator.IsAtBase ? 0 : 1500;
                    state.Set(MachineStatus.WaitingForTask, "zereginaren zain");
                    TaskRequest request = new TaskRequest();
                    request.MachineId = machineId;
                    request.MaxWaitMs = maxWait;
                    TaskAssignment assignment = link.Request<TaskAssignment>(request, MessageTypes.TaskAssignment);
                    if (assignment == null)
                    {
                        break; // konexioa galduta
                    }

                    switch (assignment.Outcome)
                    {
                        case AssignmentOutcome.Assigned:
                            executor.Execute(assignment);
                            break;
                        case AssignmentOutcome.NeedsCharge:
                            GoCharge("bateria ez da nahikoa hurrengo zereginerako (%" + state.Battery.ToString("0") + ")");
                            break;
                        case AssignmentOutcome.NoTask:
                            state.Set(MachineStatus.ReturningToBase, "aparkatzera itzultzen");
                            navigator.GoToSlot(parkingSpot);
                            state.Set(MachineStatus.Idle, null);
                            break;
                        case AssignmentOutcome.Cancelled:
                            Thread.Sleep(200);
                            break;
                    }
                }
            }
            catch (LinkLostException)
            {
                // begiztatik irteten da
            }
            Log("Zerbitzariarekiko konexioa itxita. Makina gelditu egiten da.");
        }

        // ==================================================================
        // BASERA ITZULERA ETA KARGA
        // ==================================================================

        /// <summary>
        /// Basera itzuli kargatzera:
        ///  1. Basean duen aparkalekuraino joan (fisikoki).
        ///  2. Kargagailu bat eskatu: denak okupatuta badaude, zerbitzariak ilaran uzten du (WAITING_FOR_CHARGER).
        ///  3. Esleitutako kargagailura joan eta %100 arte kargatu.
        ///  4. Bere aparkalekura itzuli eta kargagailua askatu.
        /// </summary>
        private void GoCharge(string reason)
        {
            if (battery.JustBecameLow())
            {
                SendEvent(EventKind.LowBattery, LogSeverity.Warning, "bateria baxua (%" + state.Battery.ToString("0") + ")");
            }
            Log("Basera itzultzen da: " + reason);
            SendEvent(EventKind.ReturnToBase, LogSeverity.Info, "basera itzultzen da: " + reason);

            state.BatteryStatus = BatteryStatus.ReturningToBase;
            state.Set(MachineStatus.ReturningToBase, "basera itzultzen");
            navigator.GoToSlot(parkingSpot);

            // Kargagailua eskatu (ilaran itxaron dezake)
            state.BatteryStatus = BatteryStatus.WaitingForCharger;
            state.Set(MachineStatus.WaitingForCharger, "kargagailuaren zain");
            ChargerRequest request = new ChargerRequest();
            request.MachineId = machineId;
            ChargerResponse response = link.Request<ChargerResponse>(request, MessageTypes.ChargerResponse);
            if (response == null)
            {
                throw new LinkLostException();
            }
            if (response.Outcome != ChargerOutcome.Granted)
            {
                state.BatteryStatus = BatteryStatus.LowBattery;
                state.Set(MachineStatus.Idle, null);
                return; // itxarotea bertan behera (STOP/larrialdia): begizta nagusiak kudeatzen du
            }

            // Kargagailura joan
            Log("Esleitutako kargagailua: " + response.ChargerId);
            state.Set(MachineStatus.ReturningToBase, response.ChargerId + " kargagailurantz");
            navigator.GoToSlot(layout.ChargerPosition(response.ChargerIndex));

            // Kargatu
            state.BatteryStatus = BatteryStatus.Charging;
            state.Set(MachineStatus.Charging, response.ChargerId + " kargagailuan kargatzen");
            while (state.Battery < 100)
            {
                navigator.PauseIfHalted();
                if (!link.IsConnected)
                {
                    throw new LinkLostException();
                }
                battery.Charge(0.1);
                state.UpdateTemperature(false, false, 0.1);
                Thread.Sleep(100);
            }
            state.BatteryStatus = BatteryStatus.FullyCharged;
            Log("Bateria %100ean");

            // Kargagailua libre utzi
            state.Set(MachineStatus.Idle, "kargatuta");
            navigator.GoToSlot(parkingSpot);
            ChargerRelease release = new ChargerRelease();
            release.MachineId = machineId;
            release.ChargerId = response.ChargerId;
            link.Send(release);
            state.Set(MachineStatus.Idle, null);
        }

        /// <summary>Eskuzko RETURN_TO_BASE: itzuli, behar badu kargatu eta aparkatuta geratzen da START arte.</summary>
        private void ReturnToBaseAndPark()
        {
            Log("Basera itzultzen operadorearen aginduz");
            state.Set(MachineStatus.ReturningToBase, "operadorearen agindua");
            navigator.GoToSlot(parkingSpot);

            if (state.Battery < 90)
            {
                GoCharge("operadorearen agindua");
            }

            control.SetParkedHold();
            state.Set(MachineStatus.Parked, "aparkatuta: START-en zain");
            state.BatteryStatus = state.Battery >= 99.9 ? BatteryStatus.FullyCharged : BatteryStatus.BatteryOk;
            Log("Basean aparkatuta. START-en zain.");
            control.WaitWhileParked();
            state.Set(MachineStatus.Idle, null);
            Log("START: lanera itzultzen da");
        }

        // ==================================================================
        // TELEMETRIA (hari propioa, UDP)
        // ==================================================================

        private void TelemetryLoop()
        {
            while (link.IsConnected)
            {
                link.SendTelemetry(state.ToTelemetry(machineId, token));
                Thread.Sleep(telemetryIntervalMs);
            }
        }

        private void SendEvent(EventKind kind, LogSeverity severity, string text)
        {
            MachineEventMessage message = new MachineEventMessage();
            message.MachineId = machineId;
            message.Kind = kind;
            message.Severity = severity;
            message.Text = text;
            link.Send(message);
        }

        private void Log(string text)
        {
            lock (consoleLock)
            {
                Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + name + ": " + text);
            }
        }
    }
}
