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
    /// Nekazariaren aginte-panel pertsonalizatua: BERE ustiategiaren KPIak, 3D ikuspegia,
    /// hautatutako makina, alertak eta flota osoarentzako aginduak.
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

            // Botoiak rolaren arabera (zerbitzariak beti egiaztatzen du berriro)
            if (!main.Can(Permission.ControlMachines))
            {
                SelButtons.Visibility = Visibility.Collapsed;
            }
            if (!main.Can(Permission.GlobalCommands))
            {
                GlobalButtons.Visibility = Visibility.Collapsed;
                SpeedButtons.Visibility = Visibility.Collapsed;
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
            ShowSpeed(state.SimulationSpeed);
            NoAlertsText.Visibility = state.Alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (selectedId != null && main.FindMachine(selectedId) == null)
            {
                SelectMachine(null); // makina jada ez da existitzen (ustiategi-aldaketa)
            }
            RefreshSelected();
        }

        private void UpdateKpis(FarmSummary summary)
        {
            KpiMachines.Text = summary.MachineCount.ToString();
            KpiMachinesSub.Text = summary.OnlineMachines + " konektatuta";
            KpiFields.Text = summary.FieldCount.ToString();
            KpiFieldsSub.Text = summary.FieldsCultivated + " landatuta · " + summary.FieldsPending + " egiteko";
            KpiBattery.Text = summary.AverageBattery.ToString("0") + "%";
            KpiBatteryBar.Value = summary.AverageBattery;
            KpiBatteryBar.Foreground = Texts.ToBrush(Texts.BatteryColor(summary.AverageBattery));
            KpiWorking.Text = summary.Working.ToString();
            KpiWorkingSub.Text = summary.Moving + " bidean · " + summary.Waiting + " zain";
            KpiCharging.Text = summary.Charging.ToString();
            KpiChargingSub.Text = summary.ChargersBusy + " kargagailu okupatuta · " + summary.ChargerQueue + " ilaran";
            KpiAvailable.Text = summary.Available.ToString();
            KpiAvailableSub.Text = summary.Stopped + " geldituta · " + summary.Returning + " itzultzen";
        }

        // ==================================================================
        // HAUTATUTAKO MAKINA
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

        /// <summary>Hautatutako makinaren panela berriro marrazten du (telemetria bakoitzarekin deitzen da).</summary>
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
            SelStatus.Text = row.Online ? row.StatusText.ToUpper() : "DESKONEKTATUTA";
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
        // IKUSPEGIA
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
        // AGINDUAK FLOTA OSOARI
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

        // ==================================================================
        // SIMULAZIOAREN ABIADURA (x1, x2, x5, x10)
        // ==================================================================

        /// <summary>Abiadura-botoi bat sakatzean: abiadura berria zerbitzariari bidali (botoiaren Tag-ean dago).</summary>
        private void Speed_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int speed = int.Parse((string)button.Tag);
            main.SendSimulationSpeed(speed);
            ShowSpeed(speed); // berehala nabarmendu; hurrengo FARM_STATE-k berretsiko du
        }

        /// <summary>Uneko abiaduraren botoia nabarmentzen du (berdea), besteak gris.</summary>
        private void ShowSpeed(int speed)
        {
            Button[] buttons = { Speed1, Speed2, Speed5, Speed10 };
            foreach (Button button in buttons)
            {
                bool selected = (string)button.Tag == speed.ToString();
                if (selected)
                {
                    button.ClearValue(StyleProperty); // gaiaren botoi-estilo nagusia (berdea)
                }
                else
                {
                    button.Style = (Style)FindResource("GhostButton");
                }
            }
        }
    }
}
