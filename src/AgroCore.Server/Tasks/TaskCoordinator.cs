using System;
using System.Collections.Generic;
using System.Threading;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Tasks
{
    /// <summary>
    /// Ustiategi baten ZEREGIN-KOORDINATZAILEA (ustiategi bakoitzak berea du).
    ///
    /// Soroak eta zereginak gordetzen ditu, eta erabakitzen du zein makinak egiten duen zeregin bakoitza eta noiz.
    ///
    /// SINKRONIZAZIOA (zati garrantzitsua):
    ///  - Dena lock (sync) barruan egiten da. Aldi berean hari bakar batek soilik uki ditzake soroak eta zereginak.
    ///  - Makina batek lana eskatzen duenean (TASK_REQUEST) eta ezin duenean ezer egin,
    ///    zerbitzarian duen hariak Monitor.Wait(sync) deitzen du: lo geratzen da CPUrik GASTATU GABE
    ///    eta lock-a askatzen du beste hariek lan egin dezaten.
    ///  - Zerbait aldatzen denean (zeregin bat amaitzen da, labore bat heltzen da, plan bat sortzen da...)
    ///    Monitor.PulseAll(sync) deitzen da: zain zeuden makina GUZTIAK esnatzen dira
    ///    eta bakoitzak berriro egiaztatzen du orain zereginen bat egin dezakeen.
    /// </summary>
    public class TaskCoordinator
    {
        // Zereginen kontagailu orokorra: TASK-101, TASK-102... (Interlocked = harien arteko batuketa segurua)
        private static int nextTaskNumber = 100;

        private readonly object sync = new object();
        private readonly int farmId;
        private readonly FarmLayout layout;
        private readonly Func<SystemConfig> getConfig;
        private readonly EventLogger logger;

        private readonly List<Field> fields = new List<Field>();
        private readonly List<FarmTask> tasks = new List<FarmTask>();
        private readonly List<FarmTask> finishedHistory = new List<FarmTask>();

        /// <summary>Haria une honetan WaitForTask barruan duten makinak.</summary>
        private readonly List<MachineRuntime> waitingMachines = new List<MachineRuntime>();

        public TaskCoordinator(int farmId, FarmLayout layout, List<Field> savedFields,
                               Func<SystemConfig> getConfig, EventLogger logger)
        {
            this.farmId = farmId;
            this.layout = layout;
            this.getConfig = getConfig;
            this.logger = logger;

            lock (sync)
            {
                foreach (Field field in savedFields)
                {
                    fields.Add(field);
                    RestoreTasksForField(field);
                }
                RefreshTaskStatuses();
            }
        }

        // ==================================================================
        // ZAIN DAUDEN MAKINAK ESNATU
        // ==================================================================

        /// <summary>Zain dauden hari guztiak esnatzen ditu (adibidez, STOP baten edo larrialdi baten ondoren).</summary>
        public void WakeAll()
        {
            lock (sync)
            {
                Monitor.PulseAll(sync);
            }
        }

        // ==================================================================
        // LABORE-PLANAK
        // ==================================================================

        /// <summary>Labore-plan bat sortzen du. null itzultzen du ondo badoa, edo errorearen testua.</summary>
        public string CreateCropPlan(string fieldId, CropType crop, bool autoReplant, string username)
        {
            lock (sync)
            {
                Field field = FindField(fieldId);
                if (field == null)
                {
                    return fieldId + " soroa ez dago ustiategi honetan";
                }
                if (field.Status != FieldStatus.Empty && field.Status != FieldStatus.Harvested)
                {
                    return fieldId + " soroa okupatuta dago (" + field.Status + ")";
                }
                if (HasActiveTasks(fieldId))
                {
                    return fieldId + " soroak badu dagoeneko plan bat martxan";
                }

                field.PlannedCrop = crop;
                field.AutoReplant = autoReplant;
                if (field.Status == FieldStatus.Harvested)
                {
                    ResetField(field);
                }
                CreateTasksForField(field, crop);
                RefreshTaskStatuses();

                logger.Log(EventKind.CropPlanCreated, LogSeverity.Info,
                    username + " erabiltzaileak " + CropName(crop) + " planifikatu du " + fieldId + " soroan", farmId, null, username, fieldId, null);

                Monitor.PulseAll(sync); // zeregin berriak daude: makinek egiazta dezatela
                return null;
            }
        }

        /// <summary>
        /// Labore-ziklo baten zereginak eta haien mendekotasunak sortzen ditu:
        ///
        ///   SOW ──► GROWTH ──────────────────► HARVEST
        ///    │                                    ▲
        ///    └──► IRRIGATE / SPRAY (hazkuntzaren X%-an) ─┘
        ///
        /// Hazkuntza zainketa bakoitzaren %-an gelditzen da, zainketa hori egin arte.
        /// </summary>
        private void CreateTasksForField(Field field, CropType crop)
        {
            CropDefinition definition = GetCrop(crop);

            FarmTask sow = NewTask(field, TaskType.Sow, crop);

            FarmTask growth = NewTask(field, TaskType.Growth, crop);
            growth.DependsOn.Add(sow.Id);

            FarmTask harvest = NewTask(field, TaskType.Harvest, crop);
            harvest.DependsOn.Add(growth.Id);

            foreach (CareStep step in definition.CareSteps)
            {
                FarmTask care = NewTask(field, step.Task, crop);
                care.DependsOn.Add(sow.Id);
                care.RequiredGrowth = step.AtGrowth;
                harvest.DependsOn.Add(care.Id);
            }

            // Zerrendaren ordena: ereitea, hazkuntza, zainketak, uzta-bilketa (irakurgarriagoa interfazean)
            tasks.Remove(harvest);
            tasks.Add(harvest);
        }

        private FarmTask NewTask(Field field, TaskType type, CropType crop)
        {
            FarmTask task = new FarmTask();
            task.Id = "TASK-" + Interlocked.Increment(ref nextTaskNumber);
            task.FarmId = farmId;
            task.FieldId = field.Id;
            task.Type = type;
            task.Crop = crop;
            task.Status = AgroTaskStatus.Waiting;
            tasks.Add(task);
            return task;
        }

        /// <summary>Zerbitzaria abiaraztean, zereginak berriro sortzen ditu soro bakoitzaren egoera gordearen arabera.</summary>
        private void RestoreTasksForField(Field field)
        {
            // Zerbitzaria zeregin baten erdian itxi bazen, aurreko egoera egonkorrera itzultzen da.
            if (field.Status == FieldStatus.Sowing)
            {
                field.Status = FieldStatus.Empty;
            }
            if (field.Status == FieldStatus.Harvesting)
            {
                field.Status = FieldStatus.ReadyToHarvest;
            }
            if (field.Status == FieldStatus.Harvested)
            {
                ResetField(field);
            }
            field.CurrentTaskId = null;

            if (field.Status == FieldStatus.Empty)
            {
                if (field.PlannedCrop.HasValue)
                {
                    CreateTasksForField(field, field.PlannedCrop.Value);
                }
                return;
            }

            // Growing edo ReadyToHarvest: laborea dagoeneko ereinda dago
            CropType crop = field.Crop ?? field.PlannedCrop ?? CropType.Carrot;
            CreateTasksForField(field, crop);
            foreach (FarmTask task in tasks)
            {
                if (task.FieldId != field.Id)
                {
                    continue;
                }
                if (task.Type == TaskType.Sow)
                {
                    task.Status = AgroTaskStatus.Completed;
                }
                if (task.Type == TaskType.Growth)
                {
                    task.Status = field.Status == FieldStatus.ReadyToHarvest ? AgroTaskStatus.Completed : AgroTaskStatus.Running;
                }
                if ((task.Type == TaskType.Irrigate || task.Type == TaskType.Spray) && field.CompletedCare.Contains(task.Type))
                {
                    task.Status = AgroTaskStatus.Completed;
                }
            }
        }

        // ==================================================================
        // ZEREGINEN EGOERA (mendekotasunak)
        // ==================================================================

        /// <summary>
        /// Zeregin bakoitza PENDING (prest) edo WAITING (zerbaiten zain) dagoen berriro kalkulatzen du.
        /// true itzultzen du zereginen batek egoeraz aldatu badu.
        /// </summary>
        private bool RefreshTaskStatuses()
        {
            bool changed = false;
            foreach (FarmTask task in tasks)
            {
                if (task.Status != AgroTaskStatus.Pending && task.Status != AgroTaskStatus.Waiting)
                {
                    continue;
                }
                if (task.Type == TaskType.Growth)
                {
                    task.BlockReason = "Ereitearen zain";
                    continue; // hazkuntza Tick()-ek kontrolatzen du
                }

                string reason = GetBlockReason(task);
                AgroTaskStatus newStatus = reason == null ? AgroTaskStatus.Pending : AgroTaskStatus.Waiting;
                if (newStatus != task.Status)
                {
                    changed = true;
                }
                task.Status = newStatus;
                task.BlockReason = reason;
            }
            return changed;
        }

        /// <summary>Zergatik ezin duen zeregin batek oraindik hasi (null = has daiteke).</summary>
        private string GetBlockReason(FarmTask task)
        {
            // 1. Mendekotasunak: guztiek amaituta egon behar dute
            foreach (string dependencyId in task.DependsOn)
            {
                FarmTask dependency = FindTask(dependencyId);
                if (dependency != null && dependency.Status != AgroTaskStatus.Completed)
                {
                    if (dependency.Type == TaskType.Growth)
                    {
                        Field f = FindField(task.FieldId);
                        return "Hazkuntzaren zain (" + f.Growth.ToString("0") + "%)";
                    }
                    return "Zain: " + dependency.Name;
                }
            }

            Field field = FindField(task.FieldId);

            // 2. Soroaren egoera
            if (task.Type == TaskType.Sow && field.Status != FieldStatus.Empty)
            {
                return "Soroa ez dago erabilgarri";
            }
            if (task.RequiredGrowth.HasValue && field.Growth < task.RequiredGrowth.Value - 0.01)
            {
                return "Hazkuntza " + field.Growth.ToString("0") + "% < " + task.RequiredGrowth.Value.ToString("0") + "%";
            }
            if (task.Type == TaskType.Harvest && field.Status != FieldStatus.ReadyToHarvest)
            {
                return "Laborea ez dago heldua";
            }

            // 3. Soro batean makina bakar batek egiten du lan aldi berean
            foreach (FarmTask other in tasks)
            {
                if (other != task && other.FieldId == task.FieldId &&
                    (other.Status == AgroTaskStatus.Assigned || other.Status == AgroTaskStatus.Running) &&
                    other.Type != TaskType.Growth)
                {
                    return "Soroa okupatuta: " + other.Name;
                }
            }
            return null;
        }

        // ==================================================================
        // LANA ESKATU (hemen dago Monitor.Wait)
        // ==================================================================

        /// <summary>
        /// Makina bat artatzen duen zerbitzariko hariak deitzen du, makinak TASK_REQUEST bidaltzen duenean.
        /// Zeregin egokirik ez badago, haria Monitor.Wait-ekin blokeatzen da zerbait aldatu arte.
        /// </summary>
        public TaskAssignment WaitForTask(MachineRuntime machine, int maxWaitMs, Func<bool> emergencyActive)
        {
            TaskAssignment result = new TaskAssignment();
            result.MachineId = machine.Id;
            DateTime deadline = maxWaitMs > 0 ? DateTime.Now.AddMilliseconds(maxWaitMs) : DateTime.MaxValue;
            bool waitLogged = false;

            lock (sync)
            {
                waitingMachines.Add(machine);
                try
                {
                    while (true)
                    {
                        // Itxaroteari utzi behar zaio? (STOP, larrialdia, RETURN_TO_BASE, deskonexioa)
                        if (machine.ShouldAbortWait(true) || emergencyActive())
                        {
                            result.Outcome = AssignmentOutcome.Cancelled;
                            return result;
                        }

                        // Ba al dago makina honek egin dezakeen eta egin behar duen zereginik?
                        bool batteryProblem = false;
                        FarmTask chosen = ChooseTaskFor(machine, out batteryProblem);
                        if (chosen != null)
                        {
                            Assign(chosen, machine);
                            FillAssignment(result, chosen);
                            Monitor.PulseAll(sync); // gainerako makinek berriro begiratzen dute (zeregin hau ez dago libre jada)
                            return result;
                        }
                        if (batteryProblem)
                        {
                            result.Outcome = AssignmentOutcome.NeedsCharge;
                            return result;
                        }
                        if (DateTime.Now >= deadline)
                        {
                            result.Outcome = AssignmentOutcome.NoTask;
                            return result;
                        }

                        if (!waitLogged)
                        {
                            waitLogged = true;
                            string waitingFor = DescribeWhatMachineWaitsFor(machine);
                            machine.SetNote(waitingFor);
                            if (waitingFor != null && waitingFor.StartsWith("zain FIELD"))
                            {
                                logger.Log(EventKind.Info, LogSeverity.Info,
                                    machine.Name + " " + waitingFor, farmId, machine.Id);
                            }
                        }

                        // LO EGIN norbaitek PulseAll egin arte (edo 2 s, segurtasun-sare gisa).
                        // Monitor.Wait-ek lock-a askatzen du lo dagoen bitartean, eta esnatzean berreskuratzen du.
                        Monitor.Wait(sync, 2000);
                    }
                }
                finally
                {
                    waitingMachines.Remove(machine);
                    machine.SetNote(null);
                }
            }
        }

        /// <summary>
        /// Makina baterako zeregina aukeratzen du. Makinak zeregina hartzen du soilik zain dauden bere motako
        /// makina guztien artean hautagai ONENA BADA (distantzia, abiadura, bateria, edukiera, eraginkortasuna).
        /// </summary>
        private FarmTask ChooseTaskFor(MachineRuntime machine, out bool batteryProblem)
        {
            batteryProblem = false;
            SystemConfig config = getConfig();

            foreach (FarmTask task in tasks)
            {
                if (task.Status != AgroTaskStatus.Pending)
                {
                    continue;
                }
                if (MachineMath.MachineFor(task.Type) != machine.Type)
                {
                    continue;
                }

                Field field = FindField(task.FieldId);
                if (!HasEnoughBattery(machine, field, task, config))
                {
                    batteryProblem = true; // lana badago, baina makina hau ez da iristen
                    continue;
                }

                if (IsBestCandidate(machine, task, field, config))
                {
                    batteryProblem = false;
                    return task;
                }
            }
            return null;
        }

        private bool HasEnoughBattery(MachineRuntime machine, Field field, FarmTask task, SystemConfig config)
        {
            double needed = MachineMath.BatteryNeededForTask(machine.Type, machine.Profile, config.Battery,
                layout, machine.GetPosition(), field.Index, task.Progress);
            return machine.Battery >= needed;
        }

        /// <summary>Makina hau lanaren zain dauden mota bereko gainerako makinekin alderatzen du.</summary>
        private bool IsBestCandidate(MachineRuntime machine, FarmTask task, Field field, SystemConfig config)
        {
            double myScore = Score(machine, task, field);
            foreach (MachineRuntime other in waitingMachines)
            {
                if (other == machine || other.Type != machine.Type || other.ShouldAbortWait(true))
                {
                    continue;
                }
                if (!HasEnoughBattery(other, field, task, config))
                {
                    continue;
                }
                double otherScore = Score(other, task, field);
                if (otherScore < myScore - 0.001)
                {
                    return false;
                }
                // Berdinketa: Id txikiena duenak irabazten du (horrela inoiz ez dute biek hartzen)
                if (Math.Abs(otherScore - myScore) <= 0.001 && string.CompareOrdinal(other.Id, machine.Id) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Puntuazioa (zenbat eta TXIKIAGOA, hobeto): bidaia-denbora + lan-denbora + zigorrak.</summary>
        private double Score(MachineRuntime machine, FarmTask task, Field field)
        {
            MachineProfile profile = machine.Profile;
            Position entrance = layout.CellCenter(layout.FieldEntranceCol(field.Index), layout.FieldEntranceRow(field.Index));
            double travelSeconds = MachineMath.TravelDistance(machine.Type, machine.GetPosition(), entrance) / Math.Max(0.5, profile.Speed);
            double workSeconds = MachineMath.WorkSeconds(profile, layout.FieldSize, task.Progress);
            double batteryPenalty = (100 - machine.Battery) * 0.3;
            double capacityBonus = task.Type == TaskType.Harvest ? profile.Capacity * 0.05 : 0;
            return travelSeconds + workSeconds + batteryPenalty - capacityBonus;
        }

        private void Assign(FarmTask task, MachineRuntime machine)
        {
            task.Status = AgroTaskStatus.Assigned;
            task.AssignedMachineId = machine.Id;
            task.AssignedAt = DateTime.Now;
            task.BlockReason = null;
            machine.SetCurrentTask(task.Id, task.Type, task.FieldId);
            machine.SetNote(null);
            RefreshTaskStatuses(); // soro bereko beste zereginak "soroa okupatuta" egoerara pasatzen dira

            logger.Log(EventKind.TaskAssigned, LogSeverity.Info,
                task.Id + " (" + task.Name + ") " + machine.Name + " makinari esleituta", farmId, machine.Id, null, task.FieldId, task.Type);
        }

        private void FillAssignment(TaskAssignment result, FarmTask task)
        {
            Field field = FindField(task.FieldId);
            result.Outcome = AssignmentOutcome.Assigned;
            result.TaskId = task.Id;
            result.FieldId = task.FieldId;
            result.FieldIndex = field.Index;
            result.TaskType = task.Type;
            result.Crop = task.Crop;
            result.Progress = task.Progress;
        }

        /// <summary>Panelerako testua: zeren zain dagoen makina bat.</summary>
        private string DescribeWhatMachineWaitsFor(MachineRuntime machine)
        {
            foreach (FarmTask task in tasks)
            {
                if (task.Status == AgroTaskStatus.Waiting && MachineMath.MachineFor(task.Type) == machine.Type)
                {
                    return "zain " + task.FieldId + " (" + task.BlockReason + ")";
                }
            }
            return "egiteko zereginik ez";
        }

        // ==================================================================
        // MAKINEN ABISUAK
        // ==================================================================

        public void StartTask(MachineRuntime machine, string taskId)
        {
            lock (sync)
            {
                FarmTask task = FindTask(taskId);
                if (task == null || task.AssignedMachineId != machine.Id)
                {
                    return;
                }
                task.Status = AgroTaskStatus.Running;
                task.StartedAt = DateTime.Now;

                Field field = FindField(task.FieldId);
                field.CurrentTaskId = task.Id;
                if (task.Type == TaskType.Sow)
                {
                    SetFieldStatus(field, FieldStatus.Sowing);
                }
                if (task.Type == TaskType.Harvest)
                {
                    SetFieldStatus(field, FieldStatus.Harvesting);
                }

                logger.Log(EventKind.TaskStarted, LogSeverity.Info,
                    machine.Name + " makinak hasi du " + task.Id + " (" + task.Name + ")", farmId, machine.Id, null, task.FieldId, task.Type);
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>TASK_COMPLETED: soroa eguneratzen da eta zain zeuden makinak esnatzen dira.</summary>
        public void CompleteTask(MachineRuntime machine, string taskId)
        {
            lock (sync)
            {
                FarmTask task = FindTask(taskId);
                if (task == null || task.AssignedMachineId != machine.Id || task.IsFinished())
                {
                    return;
                }

                task.Status = AgroTaskStatus.Completed;
                task.Progress = 100;
                task.CompletedAt = DateTime.Now;
                task.CompletedBy = machine.Id;
                machine.SetCurrentTask(null, null, null);
                machine.CountTaskCompleted();

                Field field = FindField(task.FieldId);
                field.CurrentTaskId = null;

                switch (task.Type)
                {
                    case TaskType.Sow:
                        // Soroa ereinda: hazten hasten da
                        field.Crop = task.Crop;
                        field.Growth = 0;
                        field.PlantedAt = DateTime.Now;
                        field.CompletedCare.Clear();
                        SetFieldStatus(field, FieldStatus.Growing);
                        FarmTask growth = FindTaskOfField(field.Id, TaskType.Growth);
                        if (growth != null)
                        {
                            growth.Status = AgroTaskStatus.Running;
                            growth.StartedAt = DateTime.Now;
                            growth.BlockReason = null;
                        }
                        break;

                    case TaskType.Irrigate:
                    case TaskType.Spray:
                        field.CompletedCare.Add(task.Type);
                        if (field.GrowthBlockedBy == task.Type)
                        {
                            field.GrowthBlockedBy = null; // laboreak hazten jarrai dezake
                        }
                        break;

                    case TaskType.Harvest:
                        field.TimesHarvested++;
                        field.HarvestedAt = DateTime.Now;
                        SetFieldStatus(field, FieldStatus.Harvested);
                        logger.Log(EventKind.FieldStateChanged, LogSeverity.Success,
                            field.Id + " uzta bilduta (" + CropName(task.Crop) + ")", farmId, machine.Id, null, field.Id, task.Type);
                        break;
                }

                logger.Log(EventKind.TaskCompleted, LogSeverity.Success,
                    machine.Name + " makinak osatu du " + task.Id + " (" + task.Name + ")", farmId, machine.Id, null, task.FieldId, task.Type);

                MoveToHistory(task);
                RefreshTaskStatuses();

                // Gakoa! Egoera aldatu da: zain zeuden makina guztiak esnatu.
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>TASK_ABORTED: makinak zeregina erdizka uzten du. PENDING-era itzultzen da aurrerapena gordeta.</summary>
        public void AbortTask(MachineRuntime machine, string taskId, double progress, string reason)
        {
            lock (sync)
            {
                FarmTask task = FindTask(taskId);
                if (task == null || task.AssignedMachineId != machine.Id || task.IsFinished())
                {
                    return;
                }
                ReleaseTask(task, progress);
                machine.SetCurrentTask(null, null, null);
                machine.CountTaskAborted();

                logger.Log(EventKind.TaskAborted, LogSeverity.Warning,
                    machine.Name + " makinak eten du " + task.Id + " " + task.Progress.ToString("0") + "%-an (" + reason + ")",
                    farmId, machine.Id, null, task.FieldId, task.Type);

                RefreshTaskStatuses();
                Monitor.PulseAll(sync); // beste makina batek jarrai dezake
            }
        }

        /// <summary>Makina bat deskonektatzen bada, bere zeregina libre geratzen da beste batentzat.</summary>
        public void MachineDisconnected(MachineRuntime machine)
        {
            lock (sync)
            {
                foreach (FarmTask task in tasks)
                {
                    if (task.AssignedMachineId == machine.Id && !task.IsFinished())
                    {
                        ReleaseTask(task, machine.TaskProgress);
                        logger.Log(EventKind.TaskAborted, LogSeverity.Warning,
                            task.Id + " askatuta: " + machine.Name + " deskonektatu da", farmId, machine.Id, null, task.FieldId, task.Type);
                    }
                }
                RefreshTaskStatuses();
                Monitor.PulseAll(sync);
            }
        }

        private void ReleaseTask(FarmTask task, double progress)
        {
            task.Status = AgroTaskStatus.Pending;
            task.AssignedMachineId = null;
            task.Interruptions++;
            task.Progress = Math.Clamp(progress, task.Progress, 99);

            Field field = FindField(task.FieldId);
            field.CurrentTaskId = null;
            if (field.Status == FieldStatus.Sowing)
            {
                SetFieldStatus(field, FieldStatus.Empty);
            }
            if (field.Status == FieldStatus.Harvesting)
            {
                SetFieldStatus(field, FieldStatus.ReadyToHarvest);
            }
        }

        // ==================================================================
        // BERTAN BEHERA UTZI (operadorea)
        // ==================================================================

        /// <summary>
        /// Zeregin bat bertan behera uzten du. Soroko zereginak elkarren mendekoak direnez, soro horren
        /// plan osoa uzten da bertan behera. Errorea edo null itzultzen du. machineToNotify: zeregina egiten ari zen makina.
        /// </summary>
        public string CancelTask(string taskId, string username, out string machineToNotify)
        {
            machineToNotify = null;
            lock (sync)
            {
                FarmTask task = FindTask(taskId);
                if (task == null)
                {
                    return taskId + " zeregina ez dago ustiategi honetan";
                }
                if (task.IsFinished())
                {
                    return "Zeregina amaituta dago jada";
                }

                Field field = FindField(task.FieldId);
                foreach (FarmTask other in new List<FarmTask>(tasks))
                {
                    if (other.FieldId != field.Id || other.IsFinished())
                    {
                        continue;
                    }
                    if (other.AssignedMachineId != null)
                    {
                        machineToNotify = other.AssignedMachineId;
                    }
                    other.Status = AgroTaskStatus.Cancelled;
                    other.CancelReason = username + " erabiltzaileak bertan behera utzia";
                    other.CompletedAt = DateTime.Now;
                    MoveToHistory(other);
                    logger.Log(EventKind.TaskCancelled, LogSeverity.Warning,
                        username + " erabiltzaileak bertan behera utzi du " + other.Id + " (" + other.Name + ")", farmId, other.AssignedMachineId, username, other.FieldId, other.Type);
                }

                // Soroa libre geratzen da
                field.PlannedCrop = null;
                field.AutoReplant = false;
                ResetField(field);

                RefreshTaskStatuses();
                Monitor.PulseAll(sync);
                return null;
            }
        }

        // ==================================================================
        // HAZKUNTZA (GrowthService hariak deitzen du segundo erdiro)
        // ==================================================================

        public void Tick(double seconds)
        {
            lock (sync)
            {
                bool changed = false;
                SystemConfig config = getConfig();

                foreach (Field field in fields)
                {
                    if (field.Status == FieldStatus.Growing && field.Crop.HasValue)
                    {
                        changed |= Grow(field, seconds);
                    }

                    // Uzta bildutako soroa libre geratzen da segundo batzuen buruan
                    if (field.Status == FieldStatus.Harvested && field.HarvestedAt.HasValue &&
                        (DateTime.Now - field.HarvestedAt.Value).TotalSeconds >= config.HarvestedResetSeconds)
                    {
                        CropType? replant = field.AutoReplant ? field.PlannedCrop : null;
                        ResetField(field);
                        if (replant.HasValue)
                        {
                            CreateTasksForField(field, replant.Value); // automatikoki birlandatu
                        }
                        else
                        {
                            field.PlannedCrop = null;
                        }
                        changed = true;
                    }
                }

                if (RefreshTaskStatuses())
                {
                    changed = true;
                }

                if (changed)
                {
                    Monitor.PulseAll(sync); // adib. labore bat heldu da: uzta-biltzaileak esnatu
                }
            }
        }

        /// <summary>Soro bat hazarazten du. true itzultzen du zerbait garrantzitsua aldatu bada.</summary>
        private bool Grow(Field field, double seconds)
        {
            CropDefinition definition = GetCrop(field.Crop.Value);
            double increment = seconds / (definition.GrowthMinutes * 60.0) * 100.0;
            double newGrowth = field.Growth + increment;

            // Ba al dago egiteko zainketaren bat puntu honen aurretik? Orduan hazkuntza hor gelditzen da.
            CareStep blocking = null;
            foreach (CareStep step in definition.CareSteps)
            {
                if (!field.CompletedCare.Contains(step.Task) && newGrowth >= step.AtGrowth)
                {
                    if (blocking == null || step.AtGrowth < blocking.AtGrowth)
                    {
                        blocking = step;
                    }
                }
            }

            bool changed = false;
            if (blocking != null)
            {
                newGrowth = Math.Max(field.Growth, blocking.AtGrowth);
                if (field.GrowthBlockedBy != blocking.Task)
                {
                    field.GrowthBlockedBy = blocking.Task;
                    string need = blocking.Task == TaskType.Irrigate ? "ureztatzea" : "fumigazioa";
                    logger.Log(EventKind.FieldStateChanged, LogSeverity.Info,
                        field.Id + " soroak " + need + " behar du (" + newGrowth.ToString("0") + "%)", farmId, null, null, field.Id, blocking.Task);
                    changed = true;
                }
            }
            field.Growth = Math.Min(100, newGrowth);

            FarmTask growthTask = FindTaskOfField(field.Id, TaskType.Growth);
            if (growthTask != null)
            {
                growthTask.Progress = field.Growth;
                growthTask.BlockReason = field.GrowthBlockedBy.HasValue ? "Behar da: " + field.GrowthBlockedBy.Value.ToString().ToUpper() : null;
            }

            if (field.Growth >= 100)
            {
                SetFieldStatus(field, FieldStatus.ReadyToHarvest);
                field.GrowthBlockedBy = null;
                if (growthTask != null)
                {
                    growthTask.Status = AgroTaskStatus.Completed;
                    growthTask.CompletedAt = DateTime.Now;
                    growthTask.CompletedBy = "SERVER";
                    MoveToHistory(growthTask);
                }
                logger.Log(EventKind.FieldStateChanged, LogSeverity.Success,
                    field.Id + " uzta biltzeko prest (" + CropName(field.Crop.Value) + ")", farmId, null, null, field.Id, TaskType.Growth);
                changed = true;
            }
            return changed;
        }

        // ==================================================================
        // KONTSULTAK (kontrol zentrora bidaltzeko kopiak)
        // ==================================================================

        public List<Field> GetFieldsSnapshot()
        {
            lock (sync)
            {
                List<Field> copy = new List<Field>();
                foreach (Field field in fields)
                {
                    copy.Add(field.Clone());
                }
                return copy;
            }
        }

        /// <summary>Zeregin aktiboak + amaitutako azkenak.</summary>
        public List<FarmTask> GetTasksSnapshot(int finishedCount)
        {
            lock (sync)
            {
                List<FarmTask> copy = new List<FarmTask>();
                foreach (FarmTask task in tasks)
                {
                    copy.Add(task.Clone());
                }
                int start = Math.Max(0, finishedHistory.Count - finishedCount);
                for (int i = finishedHistory.Count - 1; i >= start; i--)
                {
                    copy.Add(finishedHistory[i].Clone());
                }
                return copy;
            }
        }

        /// <summary>Aldi batean amaitutako zereginak (PDF txostenerako).</summary>
        public List<FarmTask> GetFinishedTasks(DateTime from, DateTime to)
        {
            lock (sync)
            {
                List<FarmTask> result = new List<FarmTask>();
                foreach (FarmTask task in finishedHistory)
                {
                    if (task.CompletedAt.HasValue && task.CompletedAt.Value >= from && task.CompletedAt.Value <= to)
                    {
                        result.Add(task.Clone());
                    }
                }
                return result;
            }
        }

        public bool HasField(string fieldId)
        {
            lock (sync)
            {
                return FindField(fieldId) != null;
            }
        }

        public bool HasTask(string taskId)
        {
            lock (sync)
            {
                return FindTask(taskId) != null;
            }
        }

        // ==================================================================
        // LAGUNTZAILEAK
        // ==================================================================

        private void SetFieldStatus(Field field, FieldStatus status)
        {
            if (field.Status == status)
            {
                return;
            }
            field.Status = status;
            logger.Log(EventKind.FieldStateChanged, LogSeverity.Debug,
                field.Id + " -> " + status, farmId, null, null, field.Id, null);
        }

        private void ResetField(Field field)
        {
            field.Status = FieldStatus.Empty;
            field.Crop = null;
            field.Growth = 0;
            field.GrowthBlockedBy = null;
            field.CompletedCare.Clear();
            field.CurrentTaskId = null;
            field.PlantedAt = null;
        }

        private void MoveToHistory(FarmTask task)
        {
            tasks.Remove(task);
            finishedHistory.Add(task);
            if (finishedHistory.Count > 1000)
            {
                finishedHistory.RemoveAt(0);
            }
        }

        private bool HasActiveTasks(string fieldId)
        {
            foreach (FarmTask task in tasks)
            {
                if (task.FieldId == fieldId && !task.IsFinished())
                {
                    return true;
                }
            }
            return false;
        }

        private Field FindField(string fieldId)
        {
            foreach (Field field in fields)
            {
                if (field.Id == fieldId)
                {
                    return field;
                }
            }
            return null;
        }

        private FarmTask FindTask(string taskId)
        {
            foreach (FarmTask task in tasks)
            {
                if (task.Id == taskId)
                {
                    return task;
                }
            }
            foreach (FarmTask task in finishedHistory)
            {
                if (task.Id == taskId)
                {
                    return task;
                }
            }
            return null;
        }

        private FarmTask FindTaskOfField(string fieldId, TaskType type)
        {
            foreach (FarmTask task in tasks)
            {
                if (task.FieldId == fieldId && task.Type == type && !task.IsFinished())
                {
                    return task;
                }
            }
            return null;
        }

        private CropDefinition GetCrop(CropType type)
        {
            foreach (CropDefinition crop in getConfig().Crops)
            {
                if (crop.Type == type)
                {
                    return crop;
                }
            }
            // konfigurazioak ez badu: balio lehenetsiak
            foreach (CropDefinition crop in CropDefinition.Defaults())
            {
                if (crop.Type == type)
                {
                    return crop;
                }
            }
            return null;
        }

        private string CropName(CropType type)
        {
            CropDefinition crop = GetCrop(type);
            return crop != null ? crop.DisplayName : type.ToString();
        }
    }
}
