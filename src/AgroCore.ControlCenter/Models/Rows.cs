using System;
using System.ComponentModel;
using System.Windows.Media;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.ControlCenter.Models
{
    /// <summary>
    /// Taulen errenkaden oinarria. Datuak aldatzen direnean Refresh() deitzen da
    /// eta WPFk propietate GUZTIAK irakurtzen ditu berriro (PropertyChanged izen hutsarekin).
    /// Horrela errenkadak eguneratzen dira taula berriro sortu gabe (hautapena ez da galtzen).
    /// </summary>
    public class Row : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Refresh()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(string.Empty));
            }
        }
    }

    // ======================================================================

    public class MachineRow : Row
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public MachineType Type { get; private set; }
        public MachineStatus Status { get; private set; }
        public bool Online { get; private set; }
        public double Battery { get; private set; }
        public double Progress { get; private set; }
        public double Speed { get; private set; }
        public double Temperature { get; private set; }
        public string CurrentTaskId { get; private set; }
        public string FieldId { get; private set; }
        public TaskType? TaskType { get; private set; }
        public string Note { get; private set; }
        public BatteryStatus BatteryStatus { get; private set; }
        public MachineProfile Profile { get; private set; }

        // Taularako testuak
        public string TypeText { get { return Texts.MachineType(Type); } }
        public string StatusText { get { return Texts.MachineStatus(Status); } }
        public Brush StatusBrush { get { return Texts.ToBrush(Texts.StatusColor(Status)); } }
        public string BatteryText { get { return Battery.ToString("0") + "%"; } }
        public Brush BatteryBrush { get { return Texts.ToBrush(Texts.BatteryColor(Battery)); } }
        public string ProgressText { get { return CurrentTaskId == null ? "" : Progress.ToString("0") + "%"; } }
        public string SpeedText { get { return Speed.ToString("0.0") + " m/s"; } }
        public string TemperatureText { get { return Temperature.ToString("0") + " ºC"; } }
        public string ModelText { get { return Profile == null ? "" : Profile.Model; } }

        public string TaskText
        {
            get
            {
                if (CurrentTaskId == null)
                {
                    return "—";
                }
                string text = TaskType.HasValue ? Texts.TaskType(TaskType.Value) : CurrentTaskId;
                if (FieldId != null)
                {
                    text = text + " · " + FieldId.Replace("FIELD-", "") + " soroa";
                }
                return text;
            }
        }

        public MachineRow(Machine machine)
        {
            Id = machine.Id;
            Update(machine);
        }

        /// <summary>Datu osoak (FARM_STATE TCP bidez, segundoro).</summary>
        public void Update(Machine machine)
        {
            Name = machine.Name;
            Type = machine.Type;
            Status = machine.Status;
            Online = machine.Online;
            Battery = machine.Battery;
            Progress = machine.TaskProgress;
            Speed = machine.Speed;
            Temperature = machine.Temperature;
            CurrentTaskId = machine.CurrentTaskId;
            FieldId = machine.CurrentFieldId;
            TaskType = machine.CurrentTaskType;
            Note = machine.Note;
            BatteryStatus = machine.BatteryStatus;
            Profile = machine.Profile;
            Refresh();
        }

        /// <summary>Datu azkarrak (UDP telemetria, 200 ms-ro).</summary>
        public void Update(Telemetry telemetry)
        {
            Battery = telemetry.Battery;
            Speed = telemetry.Speed;
            Temperature = telemetry.Temperature;
            Status = telemetry.Status;
            BatteryStatus = telemetry.BatteryStatus;
            Progress = telemetry.TaskProgress;
            Refresh();
        }
    }

    // ======================================================================

    public class FieldRow : Row
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public FieldStatus Status { get; private set; }
        public CropType? Crop { get; private set; }
        public CropType? PlannedCrop { get; private set; }
        public bool AutoReplant { get; private set; }
        public double Growth { get; private set; }
        public TaskType? BlockedBy { get; private set; }
        public int TimesHarvested { get; private set; }
        public string WorkingMachine { get; set; }

        // Erabiltzaileak txartelean aukeratzen duena plan berria sortzeko
        public int CropChoice { get; set; }
        public bool AutoReplantChoice { get; set; } = true;

        public string StatusText { get { return Texts.FieldStatus(Status); } }
        public Brush StatusBrush { get { return Texts.ToBrush(Texts.FieldStatusColor(Status)); } }
        public string CropText { get { return Texts.Crop(Crop.HasValue ? Crop : PlannedCrop); } }
        public string GrowthText { get { return Growth.ToString("0") + "%"; } }
        public string PlanText
        {
            get
            {
                if (!PlannedCrop.HasValue)
                {
                    return "Labore-planik gabe";
                }
                return "Plana: " + Texts.Crop(PlannedCrop) + (AutoReplant ? " (birlandaketa automatikoa)" : "");
            }
        }
        public string DetailText
        {
            get
            {
                string text = "";
                if (BlockedBy.HasValue)
                {
                    text = "Behar du: " + Texts.TaskType(BlockedBy.Value).ToLower();
                }
                if (WorkingMachine != null)
                {
                    text = (text == "" ? "" : text + " · ") + "Lanean: " + WorkingMachine;
                }
                return text;
            }
        }
        public string HarvestText { get { return "Uzta-bilketak: " + TimesHarvested; } }

        public FieldRow(Field field)
        {
            Id = field.Id;
            Update(field);
            if (field.PlannedCrop == CropType.Corn) CropChoice = 1;
            if (field.PlannedCrop == CropType.Tomato) CropChoice = 2;
        }

        public void Update(Field field)
        {
            Name = field.Name;
            Status = field.Status;
            Crop = field.Crop;
            PlannedCrop = field.PlannedCrop;
            AutoReplant = field.AutoReplant;
            Growth = field.Growth;
            BlockedBy = field.GrowthBlockedBy;
            TimesHarvested = field.TimesHarvested;
            Refresh();
        }
    }

    // ======================================================================

    public class TaskRow : Row
    {
        public string Id { get; private set; }
        public string FieldId { get; private set; }
        public TaskType Type { get; private set; }
        public CropType Crop { get; private set; }
        public AgroTaskStatus Status { get; private set; }
        public string AssignedMachine { get; private set; }
        public double Progress { get; private set; }
        public string BlockReason { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public int Interruptions { get; private set; }
        public bool Finished { get; private set; }

        public string TypeText { get { return Texts.TaskType(Type); } }
        public string CropText { get { return Texts.Crop(Crop); } }
        public string FieldText { get { return FieldId.Replace("FIELD-", "") + " soroa"; } }
        public string StatusText { get { return Texts.TaskStatus(Status); } }
        public Brush StatusBrush { get { return Texts.ToBrush(Texts.TaskStatusColor(Status)); } }
        public string ProgressText { get { return Progress.ToString("0") + "%"; } }
        public string MachineText { get { return AssignedMachine ?? "—"; } }
        public string CreatedText { get { return CreatedAt.ToString("HH:mm:ss"); } }
        public string CompletedText { get { return CompletedAt.HasValue ? CompletedAt.Value.ToString("HH:mm:ss") : ""; } }
        public bool CanCancel { get { return !Finished; } }

        public TaskRow(FarmTask task)
        {
            Id = task.Id;
            Update(task, null);
        }

        public void Update(FarmTask task, string machineName)
        {
            FieldId = task.FieldId;
            Type = task.Type;
            Crop = task.Crop;
            Status = task.Status;
            AssignedMachine = machineName;
            Progress = task.Progress;
            BlockReason = task.BlockReason;
            CreatedAt = task.CreatedAt;
            CompletedAt = task.CompletedAt;
            Interruptions = task.Interruptions;
            Finished = task.IsFinished();
            Refresh();
        }
    }

    // ======================================================================

    public class AlertRow
    {
        public string Time { get; set; }
        public string Text { get; set; }
        public Brush LevelBrush { get; set; }
        public string Icon { get; set; }
    }
}
