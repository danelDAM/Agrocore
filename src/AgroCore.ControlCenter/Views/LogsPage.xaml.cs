using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Views
{
    public class LogRow
    {
        public string Time { get; set; }
        public string Level { get; set; }
        public Brush LevelBrush { get; set; }
        public string Kind { get; set; }
        public string User { get; set; }
        public string Message { get; set; }
        public LogSeverity Severity { get; set; }
    }

    /// <summary>Registro de eventos de la explotación (lo guarda el servidor). Se refresca cada 5 s.</summary>
    public partial class LogsPage : UserControl
    {
        private readonly MainWindow main;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private List<LogEntry> entries = new List<LogEntry>();

        public LogsPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;

            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) => Request();
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    Request();
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                }
            };
        }

        private void Request()
        {
            LogsRequest request = new LogsRequest();
            request.FarmId = main.LastState != null ? main.LastState.FarmId : main.User.FarmId;
            request.Count = 400;
            main.Send(request);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Request();
        }

        private void OnMessage(string type, string json)
        {
            if (type == MessageTypes.LogsResponse)
            {
                LogsResponse response = MessageSerializer.FromJson<LogsResponse>(json);
                entries = response.Entries;
                ShowEntries();
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ShowEntries();
        }

        private void ShowEntries()
        {
            string search = SearchBox.Text.Trim().ToLower();
            bool onlyImportant = OnlyImportant.IsChecked == true;

            List<LogRow> rows = new List<LogRow>();
            foreach (LogEntry entry in entries)
            {
                bool important = entry.Severity == LogSeverity.Warning || entry.Severity == LogSeverity.Error
                                 || entry.Severity == LogSeverity.Security;
                if (onlyImportant && !important)
                {
                    continue;
                }
                if (search != "" && !entry.Message.ToLower().Contains(search))
                {
                    continue;
                }

                LogRow row = new LogRow();
                row.Time = entry.Timestamp.ToString("HH:mm:ss");
                row.Level = entry.Severity.ToString().ToUpper();
                row.LevelBrush = BrushFor(entry.Severity);
                row.Kind = entry.Kind.ToString();
                row.User = entry.Username ?? "";
                row.Message = entry.Message;
                row.Severity = entry.Severity;
                rows.Add(row);
            }
            LogsGrid.ItemsSource = rows;
        }

        private Brush BrushFor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Success: return (Brush)FindResource("AccentBrush");
                case LogSeverity.Warning: return (Brush)FindResource("WarningBrush");
                case LogSeverity.Error: return (Brush)FindResource("DangerBrush");
                case LogSeverity.Security: return Brushes.Orchid;
            }
            return (Brush)FindResource("MutedBrush");
        }
    }
}
