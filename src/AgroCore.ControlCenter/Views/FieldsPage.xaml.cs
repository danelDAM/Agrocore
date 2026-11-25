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
    /// Ustiategiaren soroak. PLANIFIKATZAILEA: erabiltzaileak labore bat aukeratzen du soro baterako eta
    /// zerbitzariak automatikoki sortzen ditu zereginak (ereitea, zainketak, hazkuntza eta uzta-bilketa).
    /// </summary>
    public partial class FieldsPage : UserControl
    {
        private readonly MainWindow main;

        /// <summary>Planaren kontrolak rolak laboreak kudea ditzakeenean bakarrik ikusten dira.</summary>
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
            // Zerbitzarian konfiguratutako laboreen laburpena
            string text = "Laboreak:  ";
            foreach (CropDefinition crop in state.Crops)
            {
                string care = "";
                foreach (CareStep step in crop.CareSteps)
                {
                    care = care + ", " + Texts.TaskType(step.Task).ToLower() + " (%" + step.AtGrowth.ToString("0") + ")";
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

            // Soroaren egiteko dagoen edozein zeregin bertan behera uzteak plan osoa bertan behera uzten du
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
                main.ShowStatus(row.Name + ": ez dago plan aktiborik", false);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(Window.GetWindow(this),
                row.Name + ": labore-plana bertan behera utzi? Egiteko dituen zeregin guztiak bertan behera geratuko dira.",
                "Plana bertan behera utzi", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                CancelTaskRequest request = new CancelTaskRequest();
                request.TaskId = taskId;
                main.Send(request);
            }
        }
    }
}
