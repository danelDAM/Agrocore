using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgroCore.ControlCenter.Models;
using AgroCore.ControlCenter.Scene;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Panel principal personalizado del agricultor: KPIs de SU explotación, vista 3D,
    /// máquina seleccionada, alertas y órdenes a toda la flota.
    /// </summary>
    public partial class DashboardPage : UserControl
    {
        private readonly MainWindow main;
        private string selectedId;

        public FarmScene Scene { get; private set; }

        public DashboardPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;

            Scene = new FarmScene(Viewport, LabelLayer, SceneHost);
            Scene.MachineClicked += OnMachineClicked;

            AlertsList.ItemsSource = main.AlertRows;
            main.StateUpdated += OnStateUpdated;

            // Botones según el rol (el servidor vuelve a comprobarlo siempre)
            if (!main.Can(Permission.ControlMachines))
            {
                SelButtons.Visibility = Visibility.Collapsed;
            }
            if (!main.Can(Permission.GlobalCommands))
            {
                GlobalButtons.Visibility = Visibility.Collapsed;
            }
            if (!main.Can(Permission.EmergencyStop))
            {
                EmergencyButton.Visibility = Visibility.Collapsed;
            }
            if (!main.Can(Permission.GlobalCommands) && !main.Can(Permission.EmergencyStop))
            {
                FleetCommands.Visibility = Visibility.Collapsed;
            }
        }

        private void OnStateUpdated(FarmStateMessage state)
        {
            WaitingPanel.Visibility = Visibility.Collapsed;
            Scene.UpdateState(state);
            UpdateKpis(state.Summary);
            NoAlertsText.Visibility = state.Alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (selectedId != null && main.FindMachine(selectedId) == null)
            {
                SelectMachine(null); // la máquina ya no existe (cambio de explotación)
            }
            RefreshSelected();
        }

        private void UpdateKpis(FarmSummary summary)
        {
            KpiMachines.Text = summary.MachineCount.ToString();
            KpiMachinesSub.Text = summary.OnlineMachines + " conectadas";
            KpiFields.Text = summary.FieldCount.ToString();
            KpiFieldsSub.Text = summary.FieldsCultivated + " cultivados · " + summary.FieldsPending + " pendientes";
            KpiBattery.Text = summary.AverageBattery.ToString("0") + "%";
            KpiBatteryBar.Value = summary.AverageBattery;
            KpiBatteryBar.Foreground = Texts.ToBrush(Texts.BatteryColor(summary.AverageBattery));
            KpiWorking.Text = summary.Working.ToString();
            KpiWorkingSub.Text = summary.Moving + " en camino · " + summary.Waiting + " esperando";
            KpiCharging.Text = summary.Charging.ToString();
            KpiChargingSub.Text = summary.ChargersBusy + " cargadores ocupados · " + summary.ChargerQueue + " en cola";
            KpiAvailable.Text = summary.Available.ToString();
            KpiAvailableSub.Text = summary.Stopped + " detenidas · " + summary.Returning + " volviendo";
        }

        // ==================================================================
        // MÁQUINA SELECCIONADA
        // ==================================================================

        private void OnMachineClicked(string machineId)
        {
            SelectMachine(machineId);
        }

        public void SelectMachine(string machineId)
        {
            selectedId = machineId;
            Scene.Select(machineId);
            RefreshSelected();
        }

        /// <summary>Repinta el panel de la máquina seleccionada (se llama con cada telemetría).</summary>
        public void RefreshSelected()
        {
            MachineRow row = selectedId == null ? null : main.FindMachine(selectedId);
            if (row == null)
            {
                SelectionPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
                return;
            }

            SelectionPanel.Visibility = Visibility.Visible;
            NoSelectionText.Visibility = Visibility.Collapsed;

            SelName.Text = row.Name;
            SelType.Text = row.TypeText + " · " + row.ModelText;
            SelStatus.Text = row.Online ? row.StatusText.ToUpper() : "DESCONECTADA";
            SelStatusChip.Background = row.Online ? row.StatusBrush : Brushes.Gray;
            SelBattery.Text = row.BatteryText;
            SelBatteryBar.Value = row.Battery;
            SelBatteryBar.Foreground = row.BatteryBrush;
            SelTask.Text = row.TaskText;
            SelProgress.Text = row.ProgressText;
            SelProgressBar.Value = row.CurrentTaskId == null ? 0 : row.Progress;
            SelSpeed.Text = row.SpeedText;
            SelTemperature.Text = row.TemperatureText;
            SelNote.Text = string.IsNullOrEmpty(row.Note) ? "—" : row.Note;
        }

        private void SelCommand_Click(object sender, RoutedEventArgs e)
        {
            if (selectedId != null)
            {
                main.SendMachineCommand((string)((Button)sender).Tag, selectedId);
            }
        }

        // ==================================================================
        // VISTA
        // ==================================================================

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            FollowCheck.IsChecked = false;
            Scene.FollowSelected = false;
            Scene.ResetCamera();
        }

        private void TopView_Click(object sender, RoutedEventArgs e)
        {
            FollowCheck.IsChecked = false;
            Scene.FollowSelected = false;
            Scene.TopView();
        }

        private void ViewOptions_Click(object sender, RoutedEventArgs e)
        {
            Scene.ShowLabels = LabelsCheck.IsChecked == true;
            Scene.ShowZones = ZonesCheck.IsChecked == true;
            Scene.FollowSelected = FollowCheck.IsChecked == true;
            if (Scene.FollowSelected && selectedId != null)
            {
                Scene.FocusOn(selectedId);
            }
        }

        // ==================================================================
        // ÓRDENES A TODA LA FLOTA
        // ==================================================================

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            main.SendFarmCommand(MessageTypes.StopAll);
        }

        private void ResumeAll_Click(object sender, RoutedEventArgs e)
        {
            main.SendFarmCommand(MessageTypes.ResumeAll);
        }

        private void ReturnAll_Click(object sender, RoutedEventArgs e)
        {
            main.SendFarmCommand(MessageTypes.ReturnAllToBase);
        }

        private void Emergency_Click(object sender, RoutedEventArgs e)
        {
            main.SendFarmCommand(MessageTypes.EmergencyStop);
        }
    }
}
