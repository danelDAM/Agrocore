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
    /// "Cerebro" de la máquina autónoma. Tiene dos hilos:
    ///  - Hilo principal (Run): decide qué hacer en bucle (pedir tarea, trabajar, ir a cargar...).
    ///  - Hilo de telemetría: cada 200 ms envía por UDP posición, batería, estado...
    /// Además el hilo lector de ServerLink recibe las órdenes del servidor (STOP, RESUME...).
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
        // ÓRDENES DEL SERVIDOR (se ejecuta en el hilo lector de red)
        // ==================================================================

        private void OnCommand(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.Stop:
                    Log("Orden STOP recibida");
                    control.Stop(false);
                    break;
                case MessageTypes.EmergencyStop:
                    Log("¡PARADA DE EMERGENCIA!");
                    control.Stop(true);
                    break;
                case MessageTypes.Resume:
                case MessageTypes.Start:
                    Log("Orden " + type + " recibida");
                    control.Resume();
                    break;
                case MessageTypes.ReturnToBase:
                    Log("Orden RETURN_TO_BASE recibida");
                    control.RequestReturn();
                    break;
                case MessageTypes.TaskCancelled:
                    TaskCancelledNotice notice = MessageSerializer.FromJson<TaskCancelledNotice>(json);
                    Log("Tarea " + notice.TaskId + " cancelada por el servidor");
                    control.CancelTask(notice.TaskId);
                    break;
            }
        }

        // ==================================================================
        // BUCLE PRINCIPAL
        // ==================================================================

        public void Run()
        {
            Thread telemetry = new Thread(TelemetryLoop);
            telemetry.IsBackground = true;
            telemetry.Name = "Telemetry";
            telemetry.Start();

            Log("En marcha. Batería " + state.Battery.ToString("0") + "%");

            try
            {
                while (link.IsConnected)
                {
                    navigator.PauseIfHalted();

                    // ¿Me han mandado volver a la base?
                    if (control.TakeReturnRequest())
                    {
                        ReturnToBaseAndPark();
                        continue;
                    }

                    // ¿Batería baja?
                    if (battery.IsLow())
                    {
                        GoCharge("batería baja (" + state.Battery.ToString("0") + "%)");
                        continue;
                    }

                    // Si está en la base sin hacer nada y con poca batería, aprovecha para cargar
                    if (navigator.IsAtBase && state.Battery < 60)
                    {
                        GoCharge("recarga preventiva (" + state.Battery.ToString("0") + "%)");
                        continue;
                    }

                    // Pedir trabajo al servidor. En la base espera lo que haga falta;
                    // fuera de la base espera poco y, si no hay nada, vuelve a aparcar.
                    int maxWait = navigator.IsAtBase ? 0 : 1500;
                    state.Set(MachineStatus.WaitingForTask, "esperando tarea");
                    TaskRequest request = new TaskRequest();
                    request.MachineId = machineId;
                    request.MaxWaitMs = maxWait;
                    TaskAssignment assignment = link.Request<TaskAssignment>(request, MessageTypes.TaskAssignment);
                    if (assignment == null)
                    {
                        break; // conexión perdida
                    }

                    switch (assignment.Outcome)
                    {
                        case AssignmentOutcome.Assigned:
                            executor.Execute(assignment);
                            break;
                        case AssignmentOutcome.NeedsCharge:
                            GoCharge("batería insuficiente para la siguiente tarea (" + state.Battery.ToString("0") + "%)");
                            break;
                        case AssignmentOutcome.NoTask:
                            state.Set(MachineStatus.ReturningToBase, "volviendo a aparcar");
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
                // se sale del bucle
            }
            Log("Conexión con el servidor cerrada. La máquina se detiene.");
        }

        // ==================================================================
        // RETORNO A LA BASE Y CARGA
        // ==================================================================

        /// <summary>
        /// Volver a la base a cargar:
        ///  1. Ir (físicamente) hasta su plaza en la base.
        ///  2. Pedir un cargador: si están todos ocupados, el servidor la deja en cola (WAITING_FOR_CHARGER).
        ///  3. Ir al cargador asignado y cargar hasta el 100%.
        ///  4. Volver a su plaza y liberar el cargador.
        /// </summary>
        private void GoCharge(string reason)
        {
            if (battery.JustBecameLow())
            {
                SendEvent(EventKind.LowBattery, LogSeverity.Warning, "batería baja (" + state.Battery.ToString("0") + "%)");
            }
            Log("Vuelve a la base: " + reason);
            SendEvent(EventKind.ReturnToBase, LogSeverity.Info, "vuelve a la base: " + reason);

            state.BatteryStatus = BatteryStatus.ReturningToBase;
            state.Set(MachineStatus.ReturningToBase, "volviendo a la base");
            navigator.GoToSlot(parkingSpot);

            // Pedir cargador (puede esperar en cola)
            state.BatteryStatus = BatteryStatus.WaitingForCharger;
            state.Set(MachineStatus.WaitingForCharger, "esperando cargador");
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
                return; // espera cancelada (STOP/emergencia): el bucle principal se encarga
            }

            // Ir al cargador
            Log("Cargador asignado: " + response.ChargerId);
            state.Set(MachineStatus.ReturningToBase, "hacia " + response.ChargerId);
            navigator.GoToSlot(layout.ChargerPosition(response.ChargerIndex));

            // Cargar
            state.BatteryStatus = BatteryStatus.Charging;
            state.Set(MachineStatus.Charging, "cargando en " + response.ChargerId);
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
            Log("Batería al 100%");

            // Dejar el cargador libre
            state.Set(MachineStatus.Idle, "cargada");
            navigator.GoToSlot(parkingSpot);
            ChargerRelease release = new ChargerRelease();
            release.MachineId = machineId;
            release.ChargerId = response.ChargerId;
            link.Send(release);
            state.Set(MachineStatus.Idle, null);
        }

        /// <summary>RETURN_TO_BASE manual: vuelve, carga si lo necesita y se queda aparcada hasta START.</summary>
        private void ReturnToBaseAndPark()
        {
            Log("Volviendo a la base por orden del operador");
            state.Set(MachineStatus.ReturningToBase, "orden del operador");
            navigator.GoToSlot(parkingSpot);

            if (state.Battery < 90)
            {
                GoCharge("orden del operador");
            }

            control.SetParkedHold();
            state.Set(MachineStatus.Parked, "aparcada: esperando START");
            state.BatteryStatus = state.Battery >= 99.9 ? BatteryStatus.FullyCharged : BatteryStatus.BatteryOk;
            Log("Aparcada en la base. Esperando START.");
            control.WaitWhileParked();
            state.Set(MachineStatus.Idle, null);
            Log("START: vuelve al trabajo");
        }

        // ==================================================================
        // TELEMETRÍA (hilo propio, UDP)
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
