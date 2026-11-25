using System.Windows;
using System.Windows.Controls;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.Models;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>Makinen zerrenda, denbora errealeko egoerarekin eta kontrol-botoiekin.</summary>
    public partial class MachinesPage : UserControl
    {
        private readonly MainWindow main;

        public MachinesPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            MachinesGrid.ItemsSource = main.MachineRows;

            if (!main.Can(Permission.ControlMachines))
            {
                ActionsColumn.Visibility = Visibility.Collapsed;
            }
        }

        private void Command_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            MachineRow row = (MachineRow)button.DataContext;
            main.SendMachineCommand((string)button.Tag, row.Id);
        }

        private void MachinesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MachineRow row = MachinesGrid.SelectedItem as MachineRow;
            if (row == null)
            {
                Details.Visibility = Visibility.Collapsed;
                NoSelection.Visibility = Visibility.Visible;
                return;
            }

            Details.Visibility = Visibility.Visible;
            NoSelection.Visibility = Visibility.Collapsed;
            DetName.Text = row.Name;
            DetModel.Text = row.TypeText + " · " + row.ModelText + " · " + row.Id;

            // Makinaren gaitasunak (bakoitza desberdina da)
            DetGrid.Children.Clear();
            DetGrid.RowDefinitions.Clear();
            MachineProfile p = row.Profile ?? new MachineProfile();
            AddDetail("Abiadura maximoa", p.Speed.ToString("0.0") + " m/s");
            AddDetail("Lan-abiadura", (p.Speed * p.WorkSpeedFactor).ToString("0.0") + " m/s");
            AddDetail("Kontsumoa martxan", p.MoveConsumption.ToString("0.00") + " %/s");
            AddDetail("Kontsumoa lanean", p.WorkConsumption.ToString("0.00") + " %/s");
            AddDetail("Edukiera", p.Capacity.ToString("0"));
            AddDetail("Lan-zabalera", (p.WorkRadius * 2).ToString("0.0") + " m");
            AddDetail("Eraginkortasuna", (p.Efficiency * 100).ToString("0") + "%");
            AddDetail("Bateriaren egoera", Texts.BatteryStatus(row.BatteryStatus));
        }

        private void AddDetail(string label, string value)
        {
            int rowIndex = DetGrid.RowDefinitions.Count;
            DetGrid.RowDefinitions.Add(new RowDefinition());

            TextBlock labelText = new TextBlock();
            labelText.Text = label;
            labelText.Style = (Style)FindResource("MutedText");
            labelText.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(labelText, rowIndex);

            TextBlock valueText = new TextBlock();
            valueText.Text = value;
            valueText.FontSize = 12;
            valueText.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(valueText, rowIndex);
            Grid.SetColumn(valueText, 1);

            DetGrid.Children.Add(labelText);
            DetGrid.Children.Add(valueText);
        }
    }
}
