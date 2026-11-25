using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>Fila de la tabla de estadísticas por máquina (ya con los textos formateados).</summary>
    public class MachineStatsRow
    {
        public string Name { get; set; }
        public string TypeText { get; set; }
        public string Distance { get; set; }
        public string Active { get; set; }
        public string Working { get; set; }
        public string Moving { get; set; }
        public string Waiting { get; set; }
        public string Charging { get; set; }
        public string Battery { get; set; }
        public int Tasks { get; set; }
        public int Aborted { get; set; }
        public int Charges { get; set; }
        public string Efficiency { get; set; }
    }

    /// <summary>Una barra del gráfico.</summary>
    public class ChartBar
    {
        public string Name { get; set; }
        public double BarWidth { get; set; }
        public string ValueText { get; set; }
        public Brush Color { get; set; }
    }

    /// <summary>
    /// Estadísticas globales de la explotación y de cada máquina.
    /// El servidor las calcula contando los eventos del registro (events.jsonl).
    /// </summary>
    public partial class StatisticsPage : UserControl
    {
        private readonly MainWindow main;

        public StatisticsPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    Request();
                }
            };
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Request();
        }

        private void Request()
        {
            StatisticsRequest request = new StatisticsRequest();
            request.FarmId = main.LastState != null ? main.LastState.FarmId : main.User.FarmId;
            main.Send(request);

            if (main.Can(Permission.ViewGlobalStatistics))
            {
                main.Send(new GlobalStatisticsRequest());
            }
        }

        private void OnMessage(string type, string json)
        {
            if (type == MessageTypes.StatisticsResponse)
            {
                StatisticsResponse response = MessageSerializer.FromJson<StatisticsResponse>(json);
                if (response.Success && response.Statistics != null)
                {
                    Show(response.Statistics);
                }
            }
            else if (type == MessageTypes.GlobalStatisticsResponse)
            {
                GlobalStatisticsResponse response = MessageSerializer.FromJson<GlobalStatisticsResponse>(json);
                GlobalGrid.ItemsSource = response.Farms;
                GlobalPanel.Visibility = Visibility.Visible;
            }
        }

        private void Show(FarmStatistics s)
        {
            PeriodText.Text = s.FarmName + "   ·   " + (s.From.Year < 2000 ? "Desde el inicio" : "Desde " + s.From.ToString("dd/MM/yyyy HH:mm")) + "   ·   Actualizado " + DateTime.Now.ToString("HH:mm:ss");

            TotalsGrid.Children.Clear();
            AddTotal("TAREAS COMPLETADAS", s.TasksCompleted.ToString());
            AddTotal("CAMPOS SEMBRADOS", s.FieldsSown.ToString());
            AddTotal("CAMPOS REGADOS", s.FieldsIrrigated.ToString());
            AddTotal("CAMPOS FUMIGADOS", s.FieldsSprayed.ToString());
            AddTotal("CAMPOS COSECHADOS", s.FieldsHarvested.ToString());
            AddTotal("DISTANCIA", s.DistanceKm.ToString("0.00") + " km");
            AddTotal("TIEMPO DE ACTIVIDAD", s.TotalRuntimeMinutes.ToString("0") + " min");
            AddTotal("TIEMPO ESPERANDO", s.WaitingMinutes.ToString("0") + " min");
            AddTotal("TIEMPO CARGANDO", s.ChargingMinutes.ToString("0") + " min");
            AddTotal("CONFLICTOS EVITADOS", s.ConflictsAvoided.ToString());
            AddTotal("INTERBLOQUEOS EVITADOS", s.DeadlocksAvoided.ToString());
            AddTotal("EMERGENCIAS", s.Emergencies.ToString());

            // Tabla y gráfico
            List<MachineStatsRow> rows = new List<MachineStatsRow>();
            List<ChartBar> bars = new List<ChartBar>();
            int max = 1;
            foreach (MachineStatistics m in s.Machines)
            {
                if (m.TasksCompleted > max)
                {
                    max = m.TasksCompleted;
                }
            }
            foreach (MachineStatistics m in s.Machines)
            {
                MachineStatsRow row = new MachineStatsRow();
                row.Name = m.Name;
                row.TypeText = Texts.MachineType(m.Type);
                row.Distance = m.DistanceKm.ToString("0.00") + " km";
                row.Active = m.ActiveMinutes.ToString("0.0") + " min";
                row.Working = m.WorkingMinutes.ToString("0.0") + " min";
                row.Moving = m.MovingMinutes.ToString("0.0") + " min";
                row.Waiting = m.WaitingMinutes.ToString("0.0") + " min";
                row.Charging = m.ChargingMinutes.ToString("0.0") + " min";
                row.Battery = m.BatteryConsumed.ToString("0") + " %";
                row.Tasks = m.TasksCompleted;
                row.Aborted = m.TasksAborted;
                row.Charges = m.ChargeCycles;
                row.Efficiency = m.Efficiency.ToString("0") + " %";
                rows.Add(row);

                ChartBar bar = new ChartBar();
                bar.Name = m.Name;
                bar.BarWidth = 4 + 500.0 * m.TasksCompleted / max;
                bar.ValueText = m.TasksCompleted.ToString();
                bar.Color = Texts.ToBrush(TypeColor(m.Type));
                bars.Add(bar);
            }
            MachinesGrid.ItemsSource = rows;
            ChartList.ItemsSource = bars;
        }

        private void AddTotal(string label, string value)
        {
            Border card = new Border();
            card.Style = (Style)FindResource("Card");
            card.Margin = new Thickness(0, 0, 10, 10);

            StackPanel panel = new StackPanel();
            TextBlock labelText = new TextBlock();
            labelText.Text = label;
            labelText.Style = (Style)FindResource("SectionText");
            labelText.Margin = new Thickness(0);
            TextBlock valueText = new TextBlock();
            valueText.Text = value;
            valueText.FontSize = 22;
            valueText.FontWeight = FontWeights.SemiBold;
            panel.Children.Add(labelText);
            panel.Children.Add(valueText);
            card.Child = panel;
            TotalsGrid.Children.Add(card);
        }

        private static Color TypeColor(MachineType type)
        {
            switch (type)
            {
                case MachineType.Seeder: return Color.FromRgb(70, 130, 210);
                case MachineType.Irrigator: return Color.FromRgb(40, 170, 170);
                case MachineType.Sprayer: return Color.FromRgb(160, 200, 90);
            }
            return Color.FromRgb(220, 90, 70);
        }
    }
}
