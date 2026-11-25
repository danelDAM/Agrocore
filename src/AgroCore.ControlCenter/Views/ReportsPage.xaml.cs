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
    /// Informes PDF por agricultor. Pasos:
    ///  1. Se pide REPORT_REQUEST al servidor (explotación + periodo).
    ///  2. El servidor comprueba los permisos y devuelve SOLO los datos de esa explotación.
    ///  3. Aquí se elige dónde guardarlo y se genera el PDF con QuestPDF.
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
            ResultText.Text = "Pidiendo los datos al servidor...";
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
                ResultText.Text = response.Message ?? "No se pudo generar el informe";
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Documento PDF (*.pdf)|*.pdf";
            dialog.FileName = "Informe_" + response.Report.FarmName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                ResultText.Text = "Generación cancelada.";
                return;
            }

            try
            {
                Services.PdfReportGenerator.Create(response.Report, dialog.FileName);
                lastFile = dialog.FileName;
                OpenButton.Visibility = Visibility.Visible;
                ResultText.Text = "Informe guardado en " + dialog.FileName + "  (" + response.Report.Tasks.Count
                                  + " tareas, " + response.Report.Incidents.Count + " incidencias)";
                main.ShowStatus("Informe PDF generado", true);
            }
            catch (Exception ex)
            {
                ResultText.Text = "Error al crear el PDF: " + ex.Message;
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
