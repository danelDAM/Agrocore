using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AgroCore.ControlCenter.Models;
using AgroCore.ControlCenter.Services;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Ventana principal del Centro de Control.
    ///
    /// Recibe los mensajes del servidor en el hilo lector (TCP) y en el hilo UDP, y los pasa al
    /// hilo de la interfaz con Dispatcher.BeginInvoke. Allí un switch decide qué hacer con cada tipo.
    /// Las páginas (panel, máquinas, campos...) leen los datos de las colecciones de esta ventana.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ServerConnection connection;
        private readonly TelemetryReceiver telemetry = new TelemetryReceiver();
        private readonly DispatcherTimer clock = new DispatcherTimer();
        private bool loggingOut = false;

        public LoginResponse User { get; private set; }
        public FarmStateMessage LastState { get; private set; }

        // Datos que muestran las páginas (se actualizan sin recrearse)
        public ObservableCollection<MachineRow> MachineRows { get; private set; }
        public ObservableCollection<FieldRow> FieldRows { get; private set; }
        public ObservableCollection<TaskRow> TaskRows { get; private set; }
        public ObservableCollection<AlertRow> AlertRows { get; private set; }

        // Páginas (se crean una vez y se reutilizan; así la vista 3D no se reconstruye)
        private DashboardPage dashboardPage;
        private MachinesPage machinesPage;
        private FieldsPage fieldsPage;
        private TasksPage tasksPage;
        private StatisticsPage statisticsPage;
        private ReportsPage reportsPage;
        private LogsPage logsPage;
        private ProfilePage profilePage;
        private AdminPage adminPage;

        /// <summary>Se lanza cada vez que llega un FARM_STATE nuevo (ya en el hilo de la interfaz).</summary>
        public event Action<FarmStateMessage> StateUpdated;

        /// <summary>Se lanza con cada mensaje que no gestiona la propia ventana (para las páginas).</summary>
        public event Action<string, string> MessageArrived;

        public MainWindow(ServerConnection connection, LoginResponse user)
        {
            InitializeComponent();
            this.connection = connection;
            User = user;

            MachineRows = new ObservableCollection<MachineRow>();
            FieldRows = new ObservableCollection<FieldRow>();
            TaskRows = new ObservableCollection<TaskRow>();
            AlertRows = new ObservableCollection<AlertRow>();

            // Datos del usuario
            UserNameText.Text = user.FullName;
            UserRoleText.Text = Texts.Role(user.Role);
            UserFarmText.Text = user.Role == UserRole.Admin ? "Todas las explotaciones" : user.FarmName;
            FarmTitle.Text = user.Role == UserRole.Admin ? "Administrador del sistema" : user.FarmName;

            CreatePages();

            // Reloj de la cabecera
            clock.Interval = TimeSpan.FromSeconds(1);
            clock.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            clock.Start();
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

            // Red: primero los eventos, después arrancar los hilos
            connection.MessageReceived += OnMessageReceived;
            connection.ConnectionLost += OnConnectionLost;
            telemetry.BatchReceived += OnTelemetryReceived;
            connection.StartReading();
            telemetry.Start();

            UdpSubscribeRequest subscribe = new UdpSubscribeRequest();
            subscribe.UdpPort = telemetry.Port;
            connection.Send(subscribe);

            if (user.Role == UserRole.Admin)
            {
                FarmSelectorPanel.Visibility = Visibility.Visible;
                connection.Send(new ListUsersRequest()); // la respuesta trae la lista de explotaciones
            }

            Closing += MainWindow_Closing;
        }

        // ==================================================================
        // PERMISOS (los comprueba también el servidor; aquí solo se ocultan botones)
        // ==================================================================

        public bool Can(Permission permission)
        {
            return RolePermissions.Has(User.Role, permission);
        }

        // ==================================================================
        // NAVEGACIÓN
        // ==================================================================

        private void CreatePages()
        {
            dashboardPage = new DashboardPage(this);
            machinesPage = new MachinesPage(this);
            fieldsPage = new FieldsPage(this);
            tasksPage = new TasksPage(this);
            statisticsPage = new StatisticsPage(this);
            reportsPage = new ReportsPage(this);
            logsPage = new LogsPage(this);
            profilePage = new ProfilePage(this);
            adminPage = new AdminPage(this);

            // Opciones del menú según el rol
            NavStatistics.Visibility = Can(Permission.ViewStatistics) ? Visibility.Visible : Visibility.Collapsed;
            NavReports.Visibility = Can(Permission.GenerateReports) ? Visibility.Visible : Visibility.Collapsed;
            NavLogs.Visibility = Can(Permission.ViewLogs) ? Visibility.Visible : Visibility.Collapsed;
            NavAdmin.Visibility = Can(Permission.ManageUsers) ? Visibility.Visible : Visibility.Collapsed;
            if (!Can(Permission.ViewStatistics) && !Can(Permission.GenerateReports) && !Can(Permission.ViewLogs))
            {
                AnalysisHeader.Visibility = Visibility.Collapsed;
            }
            NavDashboard.IsChecked = true;
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageHost == null)
            {
                return; // todavía cargando la ventana
            }
            RadioButton button = (RadioButton)sender;
            PageTitle.Text = button.Content.ToString();

            if (button == NavDashboard) PageHost.Content = dashboardPage;
            else if (button == NavMachines) PageHost.Content = machinesPage;
            else if (button == NavFields) PageHost.Content = fieldsPage;
            else if (button == NavTasks) PageHost.Content = tasksPage;
            else if (button == NavStatistics) PageHost.Content = statisticsPage;
            else if (button == NavReports) PageHost.Content = reportsPage;
            else if (button == NavLogs) PageHost.Content = logsPage;
            else if (button == NavProfile) PageHost.Content = profilePage;
            else if (button == NavAdmin) PageHost.Content = adminPage;
        }

        /// <summary>Abre la página de máquinas con una máquina seleccionada (desde el panel).</summary>
        public void ShowMachines()
        {
            NavMachines.IsChecked = true;
        }

        // ==================================================================
        // ENVÍO DE ÓRDENES
        // ==================================================================

        public void Send(NetMessage message)
        {
            if (!connection.Send(message))
            {
                ShowStatus("No se pudo enviar la orden: sin conexión con el servidor", false);
            }
        }

        public void SendMachineCommand(string type, string machineId)
        {
            Send(new MachineCommand(type, machineId));
        }

        public void SendFarmCommand(string type)
        {
            string question = null;
            if (type == MessageTypes.EmergencyStop)
            {
                question = "¿Activar la PARADA DE EMERGENCIA? Todas las máquinas se detendrán inmediatamente.";
            }
            else if (type == MessageTypes.StopAll)
            {
                question = "¿Detener todas las máquinas de la explotación?";
            }
            else if (type == MessageTypes.ReturnAllToBase)
            {
                question = "¿Enviar todas las máquinas a la base?";
            }

            if (question != null)
            {
                MessageBoxResult answer = MessageBox.Show(this, question, "Confirmar orden", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            FarmCommand command = new FarmCommand(type);
            command.FarmId = LastState != null ? LastState.FarmId : User.FarmId;
            Send(command);
        }

        public void ShowStatus(string text, bool ok)
        {
            StatusText.Text = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
            StatusText.Foreground = ok ? (Brush)FindResource("MutedBrush") : (Brush)FindResource("DangerBrush");
        }

        // ==================================================================
        // MENSAJES DEL SERVIDOR
        // ==================================================================

        /// <summary>Hilo lector TCP: solo pasa el mensaje al hilo de la interfaz.</summary>
        private void OnMessageReceived(string type, string json)
        {
            Dispatcher.BeginInvoke(new Action(() => HandleMessage(type, json)));
        }

        /// <summary>Hilo de la interfaz: aquí ya se pueden tocar los controles.</summary>
        private void HandleMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.FarmState:
                    FarmStateMessage state = MessageSerializer.FromJson<FarmStateMessage>(json);
                    ApplyState(state);
                    break;

                case MessageTypes.Result:
                    OperationResult result = MessageSerializer.FromJson<OperationResult>(json);
                    if (result.RequestType != MessageTypes.UdpSubscribe)
                    {
                        ShowStatus(result.Message, result.Success);
                    }
                    break;

                case MessageTypes.UsersResponse:
                    ListUsersResponse users = MessageSerializer.FromJson<ListUsersResponse>(json);
                    FillFarmSelector(users.Farms);
                    break;

                case MessageTypes.ForceLogout:
                    ForceLogoutNotice notice = MessageSerializer.FromJson<ForceLogoutNotice>(json);
                    EndSession("Sesión cerrada por el servidor: " + notice.Reason, false);
                    return;
            }

            // Las páginas pueden querer el mensaje (estadísticas, informes, usuarios...)
            if (MessageArrived != null)
            {
                MessageArrived(type, json);
            }
        }

        /// <summary>Hilo UDP: telemetría cada 200 ms.</summary>
        private void OnTelemetryReceived(TelemetryBatch batch)
        {
            Dispatcher.BeginInvoke(new Action(() => ApplyTelemetry(batch)));
        }

        private void ApplyTelemetry(TelemetryBatch batch)
        {
            if (LastState == null || batch.FarmId != LastState.FarmId)
            {
                return;
            }
            foreach (Telemetry item in batch.Items)
            {
                MachineRow row = FindMachine(item.MachineId);
                if (row != null && row.Online)
                {
                    row.Update(item);
                }
                dashboardPage.Scene.ApplyTelemetry(item);
            }
            dashboardPage.RefreshSelected();
        }

        private void OnConnectionLost()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!loggingOut)
                {
                    EndSession("Se ha perdido la conexión con el servidor. Vuelva a iniciar sesión.", false);
                }
            }));
        }

        // ==================================================================
        // ESTADO DE LA EXPLOTACIÓN
        // ==================================================================

        private void ApplyState(FarmStateMessage state)
        {
            bool farmChanged = LastState == null || LastState.FarmId != state.FarmId;
            LastState = state;

            if (farmChanged)
            {
                MachineRows.Clear();
                FieldRows.Clear();
                TaskRows.Clear();
            }

            FarmTitle.Text = state.FarmName + "  ·  " + state.OwnerName + "  ·  " + state.FieldCount + " campos, "
                             + state.Machines.Count + " máquinas, " + state.ChargerCount + " cargadores";
            EmergencyBanner.Visibility = state.EmergencyActive ? Visibility.Visible : Visibility.Collapsed;
            StatsText.Text = "Tareas completadas: " + state.Summary.TasksCompleted
                             + "   ·   Conflictos evitados: " + state.Summary.ConflictsAvoided
                             + "   ·   Cola de carga: " + state.Summary.ChargerQueue;

            SyncMachines(state);
            SyncFields(state);
            SyncTasks(state);
            SyncAlerts(state);

            if (StateUpdated != null)
            {
                StateUpdated(state);
            }
        }

        private void SyncMachines(FarmStateMessage state)
        {
            List<string> ids = new List<string>();
            foreach (Machine machine in state.Machines)
            {
                ids.Add(machine.Id);
                MachineRow row = FindMachine(machine.Id);
                if (row == null)
                {
                    MachineRows.Add(new MachineRow(machine));
                }
                else
                {
                    row.Update(machine);
                }
            }
            for (int i = MachineRows.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(MachineRows[i].Id))
                {
                    MachineRows.RemoveAt(i);
                }
            }
        }

        private void SyncFields(FarmStateMessage state)
        {
            foreach (Field field in state.Fields)
            {
                FieldRow row = null;
                foreach (FieldRow existing in FieldRows)
                {
                    if (existing.Id == field.Id)
                    {
                        row = existing;
                    }
                }

                // ¿Qué máquina está trabajando en este campo?
                string working = null;
                foreach (Machine machine in state.Machines)
                {
                    if (machine.CurrentFieldId == field.Id && machine.Online)
                    {
                        working = machine.Name;
                    }
                }

                if (row == null)
                {
                    row = new FieldRow(field);
                    FieldRows.Add(row);
                }
                row.WorkingMachine = working;
                row.Update(field);
            }
        }

        private void SyncTasks(FarmStateMessage state)
        {
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (Machine machine in state.Machines)
            {
                names[machine.Id] = machine.Name;
            }

            List<string> ids = new List<string>();
            foreach (FarmTask task in state.Tasks)
            {
                if (task.Type == TaskType.Growth)
                {
                    continue; // el crecimiento lo hace el servidor solo; se ve en la página de campos
                }
                ids.Add(task.Id);

                string machineName = null;
                if (task.AssignedMachineId != null)
                {
                    names.TryGetValue(task.AssignedMachineId, out machineName);
                    if (machineName == null)
                    {
                        machineName = task.CompletedBy ?? task.AssignedMachineId;
                    }
                }
                else if (task.CompletedBy != null)
                {
                    names.TryGetValue(task.CompletedBy, out machineName);
                }

                TaskRow row = null;
                foreach (TaskRow existing in TaskRows)
                {
                    if (existing.Id == task.Id)
                    {
                        row = existing;
                    }
                }
                if (row == null)
                {
                    row = new TaskRow(task);
                    TaskRows.Insert(0, row); // las más nuevas arriba
                }
                row.Update(task, machineName);
            }
            for (int i = TaskRows.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(TaskRows[i].Id))
                {
                    TaskRows.RemoveAt(i);
                }
            }
        }

        private void SyncAlerts(FarmStateMessage state)
        {
            AlertRows.Clear();
            foreach (Alert alert in state.Alerts)
            {
                AlertRow row = new AlertRow();
                row.Time = alert.Timestamp.ToString("HH:mm:ss");
                row.Text = alert.Text;
                switch (alert.Level)
                {
                    case AlertLevel.Error:
                        row.LevelBrush = (Brush)FindResource("DangerBrush");
                        row.Icon = "";
                        break;
                    case AlertLevel.Warning:
                        row.LevelBrush = (Brush)FindResource("WarningBrush");
                        row.Icon = "";
                        break;
                    case AlertLevel.Success:
                        row.LevelBrush = (Brush)FindResource("AccentBrush");
                        row.Icon = "";
                        break;
                    default:
                        row.LevelBrush = (Brush)FindResource("InfoBrush");
                        row.Icon = "";
                        break;
                }
                AlertRows.Add(row);
            }
        }

        public MachineRow FindMachine(string id)
        {
            foreach (MachineRow row in MachineRows)
            {
                if (row.Id == id)
                {
                    return row;
                }
            }
            return null;
        }

        // ==================================================================
        // SELECTOR DE EXPLOTACIÓN (solo administrador)
        // ==================================================================

        private bool fillingFarms = false;

        private void FillFarmSelector(List<FarmDto> farms)
        {
            int currentId = LastState != null ? LastState.FarmId : 0;
            fillingFarms = true;
            FarmSelector.Items.Clear();
            foreach (FarmDto farm in farms)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = farm.Name + " (" + farm.OwnerName + ")";
                item.Tag = farm.Id;
                FarmSelector.Items.Add(item);
                if (farm.Id == currentId)
                {
                    FarmSelector.SelectedItem = item;
                }
            }
            fillingFarms = false;
        }

        private void FarmSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = FarmSelector.SelectedItem as ComboBoxItem;
            if (item == null || fillingFarms)
            {
                return;
            }
            SelectFarmRequest request = new SelectFarmRequest();
            request.FarmId = (int)item.Tag;
            Send(request);
        }

        /// <summary>Vuelve a pedir la lista de explotaciones (p. ej. tras crear un agricultor).</summary>
        public void RefreshFarmList()
        {
            if (User.Role == UserRole.Admin)
            {
                Send(new ListUsersRequest());
            }
        }

        // ==================================================================
        // CERRAR SESIÓN
        // ==================================================================

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            connection.Send(new LogoutRequest()); // avisar al servidor
            EndSession("Sesión cerrada correctamente.", true);
        }

        /// <summary>Cierra la sesión: borra la sesión local, cierra la conexión y vuelve al login.</summary>
        private void EndSession(string notice, bool normalLogout)
        {
            if (loggingOut)
            {
                return;
            }
            loggingOut = true;

            LocalSession.Clear();
            telemetry.Stop();
            connection.Close();
            clock.Stop();
            dashboardPage.Scene.Dispose();

            ((App)Application.Current).ShowLogin(notice);
            Closing -= MainWindow_Closing;
            Close();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cerrar la ventana con la X = cerrar sesión y salir del programa
            loggingOut = true;
            connection.Send(new LogoutRequest());
            LocalSession.Clear();
            telemetry.Stop();
            connection.Close();
            dashboardPage.Scene.Dispose();
            Application.Current.Shutdown();
        }
    }
}
