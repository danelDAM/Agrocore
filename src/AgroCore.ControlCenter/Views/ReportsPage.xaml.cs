using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;
using Microsoft.Win32;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// PDF txostenak nekazariz nekazari. Urratsak:
    ///  1. REPORT_REQUEST eskatzen zaio zerbitzariari (ustiategia + aldia).
    ///  2. Zerbitzariak baimenak egiaztatzen ditu eta ustiategi horren datuak BAKARRIK itzultzen ditu.
    ///  3. Hemen non gorde aukeratzen da eta PDFa QuestPDF-rekin sortzen da.
    /// </summary>
    public partial class ReportsPage : UserControl
    {
        private readonly MainWindow main;
        private string lastFile;

        public ReportsPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;
            main.StateUpdated += state =>
            {
                FarmerText.Text = state.OwnerName;
                FarmText.Text = state.FarmName;
            };
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (main.LastState == null)
            {
                return;
            }

            DateTime to = DateTime.Now;
            DateTime from;
            switch (PeriodBox.SelectedIndex)
            {
                case 0: from = to.AddHours(-1); break;
                case 1: from = DateTime.Today; break;
                case 2: from = to.AddDays(-7); break;
                case 3: from = to.AddDays(-30); break;
                default: from = new DateTime(2000, 1, 1); break;
            }

            ReportRequest request = new ReportRequest();
            request.FarmId = main.LastState.FarmId;
            request.From = from;
            request.To = to;
            main.Send(request);

            GenerateButton.IsEnabled = false;
            ResultText.Text = "Datuak zerbitzariari eskatzen...";
        }

        private void OnMessage(string type, string json)
        {
            if (type == MessageTypes.Result)
            {
                OperationResult result = MessageSerializer.FromJson<OperationResult>(json);
                if (result.RequestType == MessageTypes.ReportRequest && !result.Success)
                {
                    GenerateButton.IsEnabled = true;
                    ResultText.Text = result.Message;
                }
                return;
            }
            if (type != MessageTypes.ReportResponse)
            {
                return;
            }

            GenerateButton.IsEnabled = true;
            ReportResponse response = MessageSerializer.FromJson<ReportResponse>(json);
            if (!response.Success || response.Report == null)
            {
                ResultText.Text = response.Message ?? "Ezin izan da txostena sortu";
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "PDF dokumentua (*.pdf)|*.pdf";
            dialog.FileName = "Txostena_" + response.Report.FarmName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                ResultText.Text = "Sorrera bertan behera utzi da.";
                return;
            }

            try
            {
                Services.PdfReportGenerator.Create(response.Report, dialog.FileName);
                lastFile = dialog.FileName;
                OpenButton.Visibility = Visibility.Visible;
                ResultText.Text = "Txostena hemen gorde da: " + dialog.FileName + "  (" + response.Report.Tasks.Count
                                  + " zeregin, " + response.Report.Incidents.Count + " gorabehera)";
                main.ShowStatus("PDF txostena sortu da", true);
            }
            catch (Exception ex)
            {
                ResultText.Text = "Errorea PDFa sortzean: " + ex.Message;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (lastFile != null && File.Exists(lastFile))
            {
                ProcessStartInfo info = new ProcessStartInfo(lastFile);
                info.UseShellExecute = true;
                Process.Start(info);
            }
        }
    }
}
