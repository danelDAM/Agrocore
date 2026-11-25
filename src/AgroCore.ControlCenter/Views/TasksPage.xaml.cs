using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>Lista de tareas con su estado (PENDING, WAITING, ASSIGNED, RUNNING, COMPLETED, CANCELLED).</summary>
    public partial class TasksPage : UserControl
    {
        private readonly MainWindow main;
        private readonly ICollectionView view;

        public TasksPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;

            // Vista filtrada de la colección: por defecto oculta las tareas terminadas
            view = CollectionViewSource.GetDefaultView(main.TaskRows);
            view.Filter = Filter;
            TasksGrid.ItemsSource = view;

            if (!main.Can(Permission.ManageTasks))
            {
                CancelColumn.Visibility = Visibility.Collapsed;
            }
            main.StateUpdated += OnStateUpdated;
        }

        private bool Filter(object item)
        {
            TaskRow row = (TaskRow)item;
            return ShowFinished.IsChecked == true || !row.Finished;
        }

        private void OnStateUpdated(FarmStateMessage state)
        {
            int running = 0, waiting = 0, completed = 0, cancelled = 0;
            foreach (TaskRow row in main.TaskRows)
            {
                if (row.Status == AgroTaskStatus.Running || row.Status == AgroTaskStatus.Assigned) running++;
                if (row.Status == AgroTaskStatus.Waiting || row.Status == AgroTaskStatus.Pending) waiting++;
                if (row.Status == AgroTaskStatus.Completed) completed++;
                if (row.Status == AgroTaskStatus.Cancelled) cancelled++;
            }
            CountsText.Text = "En curso: " + running + "    Pendientes / en espera: " + waiting
                              + "    Completadas: " + completed + "    Canceladas: " + cancelled;

            if (IsVisible)
            {
                view.Refresh(); // vuelve a aplicar el filtro (alguna tarea puede haber terminado)
            }
        }

        private void ShowFinished_Click(object sender, RoutedEventArgs e)
        {
            view.Refresh();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            TaskRow row = (TaskRow)((Button)sender).DataContext;
            MessageBoxResult answer = MessageBox.Show(Window.GetWindow(this),
                "¿Cancelar " + row.Id + " (" + row.TypeText + " en " + row.FieldText + ")?\n" +
                "Se cancelará el plan de cultivo completo de ese campo.",
                "Cancelar tarea", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                CancelTaskRequest request = new CancelTaskRequest();
                request.TaskId = row.Id;
                main.Send(request);
            }
        }
    }
}
