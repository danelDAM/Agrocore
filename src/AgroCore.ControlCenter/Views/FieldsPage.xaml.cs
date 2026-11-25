using System.Windows;
using System.Windows.Controls;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Campos de la explotación. El PLANIFICADOR: el usuario elige un cultivo para un campo y el
    /// servidor genera automáticamente las tareas (siembra, cuidados, crecimiento y cosecha).
    /// </summary>
    public partial class FieldsPage : UserControl
    {
        private readonly MainWindow main;

        /// <summary>Los controles del plan solo se ven si el rol puede gestionar cultivos.</summary>
        public Visibility PlanControlsVisibility { get; private set; }

        public FieldsPage(MainWindow main)
        {
            this.main = main;
            PlanControlsVisibility = main.Can(Permission.ManageCropPlans) ? Visibility.Visible : Visibility.Collapsed;
            InitializeComponent();

            FieldsList.DataContext = this;
            FieldsList.ItemsSource = main.FieldRows;
            main.StateUpdated += OnStateUpdated;
        }

        private void OnStateUpdated(FarmStateMessage state)
        {
            // Resumen de los cultivos configurados en el servidor
            string text = "Cultivos:  ";
            foreach (CropDefinition crop in state.Crops)
            {
                string care = "";
                foreach (CareStep step in crop.CareSteps)
                {
                    care = care + ", " + Texts.TaskType(step.Task).ToLower() + " al " + step.AtGrowth.ToString("0") + "%";
                }
                text = text + Texts.Crop(crop.Type) + " (" + crop.GrowthMinutes.ToString("0.#") + " min" + care + ")     ";
            }
            CropsInfo.Text = text;
        }

        private void Plant_Click(object sender, RoutedEventArgs e)
        {
            FieldRow row = (FieldRow)((Button)sender).DataContext;

            CropPlanRequest request = new CropPlanRequest();
            request.FieldId = row.Id;
            request.Crop = row.CropChoice == 1 ? CropType.Corn : (row.CropChoice == 2 ? CropType.Tomato : CropType.Carrot);
            request.AutoReplant = row.AutoReplantChoice;
            main.Send(request);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            FieldRow row = (FieldRow)((Button)sender).DataContext;
            if (main.LastState == null)
            {
                return;
            }

            // Cancelar cualquier tarea pendiente del campo cancela el plan entero
            string taskId = null;
            foreach (FarmTask task in main.LastState.Tasks)
            {
                if (task.FieldId == row.Id && !task.IsFinished())
                {
                    taskId = task.Id;
                }
            }
            if (taskId == null)
            {
                main.ShowStatus(row.Name + " no tiene ningún plan activo", false);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(Window.GetWindow(this),
                "¿Cancelar el plan de cultivo de " + row.Name + "? Se cancelarán todas sus tareas pendientes.",
                "Cancelar plan", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                CancelTaskRequest request = new CancelTaskRequest();
                request.TaskId = taskId;
                main.Send(request);
            }
        }
    }
}
