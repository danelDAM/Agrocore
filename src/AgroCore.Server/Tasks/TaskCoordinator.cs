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
    /// COORDINADOR DE TAREAS de una explotación (hay uno por explotación).
    ///
    /// Guarda los campos y las tareas, y decide qué máquina hace cada tarea y cuándo.
    ///
    /// SINCRONIZACIÓN (la parte importante):
    ///  - Todo se hace dentro de lock (sync). Solo un hilo a la vez puede tocar campos y tareas.
    ///  - Cuando una máquina pide trabajo (TASK_REQUEST) y no hay nada que pueda hacer,
    ///    su hilo del servidor llama a Monitor.Wait(sync): se queda dormido SIN gastar CPU
    ///    y suelta el lock para que otros hilos puedan trabajar.
    ///  - Cuando algo cambia (una tarea termina, un cultivo madura, se crea un plan...)
    ///    se llama a Monitor.PulseAll(sync): se despiertan TODAS las máquinas que esperaban
    ///    y cada una vuelve a comprobar si ahora puede hacer alguna tarea.
    /// </summary>
    public class TaskCoordinator
    {
        // Contador global de tareas: TASK-101, TASK-102... (Interlocked = suma segura entre hilos)
        private static int nextTaskNumber = 100;

        private readonly object sync = new object();
        private readonly int farmId;
        private readonly FarmLayout layout;
        private readonly Func<SystemConfig> getConfig;
        private readonly EventLogger logger;

        private readonly List<Field> fields = new List<Field>();
        private readonly List<FarmTask> tasks = new List<FarmTask>();
        private readonly List<FarmTask> finishedHistory = new List<FarmTask>();

        /// <summary>Máquinas cuyo hilo está ahora mismo dentro de WaitForTask.</summary>
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
        // DESPERTAR A LAS MÁQUINAS QUE ESPERAN
        // ==================================================================

        /// <summary>Despierta a todos los hilos que esperan (por ejemplo tras un STOP o una emergencia).</summary>
        public void WakeAll()
        {
            lock (sync)
            {
                Monitor.PulseAll(sync);
            }
        }

        // ==================================================================
        // PLANES DE CULTIVO
        // ==================================================================

        /// <summary>Crea un plan de cultivo. Devuelve null si va bien o el texto del error.</summary>
        public string CreateCropPlan(string fieldId, CropType crop, bool autoReplant, string username)
        {
            lock (sync)
            {
                Field field = FindField(fieldId);
                if (field == null)
                {
                    return "El campo " + fieldId + " no existe en esta explotación";
                }
                if (field.Status != FieldStatus.Empty && field.Status != FieldStatus.Harvested)
                {
                    return "El campo " + fieldId + " está ocupado (" + field.Status + ")";
                }
                if (HasActiveTasks(fieldId))
                {
                    return "El campo " + fieldId + " ya tiene un plan en curso";
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
                    username + " planificó " + CropName(crop) + " en " + fieldId, farmId, null, username, fieldId, null);

                Monitor.PulseAll(sync); // hay tareas nuevas: que las máquinas lo comprueben
                return null;
            }
        }

        /// <summary>
        /// Genera las tareas de un ciclo de cultivo y sus dependencias:
        ///
        ///   SOW ──► GROWTH ──────────────────► HARVEST
        ///    │                                    ▲
        ///    └──► IRRIGATE / SPRAY (al X% de crecimiento) ─┘
        ///
        /// El crecimiento se para en el % de cada cuidado hasta que se hace ese cuidado.
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

            // Orden de la lista: siembra, crecimiento, cuidados, recolección (más legible en la interfaz)
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

        /// <summary>Al arrancar el servidor, vuelve a crear las tareas según el estado guardado de cada campo.</summary>
        private void RestoreTasksForField(Field field)
        {
            // Si el servidor se cerró en mitad de una tarea, se vuelve al estado estable anterior.
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

            // Growing o ReadyToHarvest: el cultivo ya está sembrado
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
        // ESTADO DE LAS TAREAS (dependencias)
        // ==================================================================

        /// <summary>
        /// Recalcula si cada tarea está PENDING (lista) o WAITING (esperando algo).
        /// Devuelve true si alguna tarea ha cambiado de estado.
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
                    task.BlockReason = "Espera a la siembra";
                    continue; // el crecimiento lo controla Tick()
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

        /// <summary>Por qué una tarea no puede empezar todavía (null = puede empezar).</summary>
        private string GetBlockReason(FarmTask task)
        {
            // 1. Dependencias: todas tienen que estar terminadas
            foreach (string dependencyId in task.DependsOn)
            {
                FarmTask dependency = FindTask(dependencyId);
                if (dependency != null && dependency.Status != AgroTaskStatus.Completed)
                {
                    if (dependency.Type == TaskType.Growth)
                    {
                        Field f = FindField(task.FieldId);
                        return "Esperando crecimiento (" + f.Growth.ToString("0") + "%)";
                    }
                    return "Esperando " + dependency.Name;
                }
            }

            Field field = FindField(task.FieldId);

            // 2. Estado del campo
            if (task.Type == TaskType.Sow && field.Status != FieldStatus.Empty)
            {
                return "Campo no disponible";
            }
            if (task.RequiredGrowth.HasValue && field.Growth < task.RequiredGrowth.Value - 0.01)
            {
                return "Crecimiento " + field.Growth.ToString("0") + "% < " + task.RequiredGrowth.Value.ToString("0") + "%";
            }
            if (task.Type == TaskType.Harvest && field.Status != FieldStatus.ReadyToHarvest)
            {
                return "Cultivo no maduro";
            }

            // 3. En un campo solo trabaja una máquina a la vez
            foreach (FarmTask other in tasks)
            {
                if (other != task && other.FieldId == task.FieldId &&
                    (other.Status == AgroTaskStatus.Assigned || other.Status == AgroTaskStatus.Running) &&
                    other.Type != TaskType.Growth)
                {
                    return "Campo ocupado por " + other.Name;
                }
            }
            return null;
        }

        // ==================================================================
        // PEDIR TRABAJO (aquí está el Monitor.Wait)
        // ==================================================================

        /// <summary>
        /// La llama el hilo del servidor que atiende a una máquina cuando ésta envía TASK_REQUEST.
        /// Si no hay una tarea adecuada, el hilo se bloquea con Monitor.Wait hasta que algo cambie.
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
                        // ¿Hay que dejar de esperar? (STOP, emergencia, RETURN_TO_BASE, desconexión)
                        if (machine.ShouldAbortWait(true) || emergencyActive())
                        {
                            result.Outcome = AssignmentOutcome.Cancelled;
                            return result;
                        }

                        // ¿Hay alguna tarea que esta máquina pueda y deba hacer?
                        bool batteryProblem = false;
                        FarmTask chosen = ChooseTaskFor(machine, out batteryProblem);
                        if (chosen != null)
                        {
                            Assign(chosen, machine);
                            FillAssignment(result, chosen);
                            Monitor.PulseAll(sync); // las demás máquinas vuelven a mirar (esta tarea ya no está libre)
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
                            if (waitingFor != null && waitingFor.StartsWith("esperando FIELD"))
                            {
                                logger.Log(EventKind.Info, LogSeverity.Info,
                                    machine.Name + " " + waitingFor, farmId, machine.Id);
                            }
                        }

                        // DORMIR hasta que alguien haga PulseAll (o 2 s como red de seguridad).
                        // Monitor.Wait suelta el lock mientras duerme y lo recupera al despertar.
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
        /// Elige la tarea para una máquina. Solo se la queda si ES LA MEJOR candidata de todas las
        /// máquinas de su tipo que están esperando (distancia, velocidad, batería, capacidad, eficiencia).
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
                    batteryProblem = true; // hay trabajo, pero esta máquina no llega
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

        /// <summary>Compara esta máquina con las demás del mismo tipo que también esperan trabajo.</summary>
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
                // Empate: gana la de Id menor (así nunca se la quedan dos)
                if (Math.Abs(otherScore - myScore) <= 0.001 && string.CompareOrdinal(other.Id, machine.Id) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Puntuación (cuanto MENOR, mejor): tiempo de viaje + tiempo de trabajo + penalizaciones.</summary>
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
            RefreshTaskStatuses(); // otras tareas del mismo campo pasan a "campo ocupado"

            logger.Log(EventKind.TaskAssigned, LogSeverity.Info,
                task.Id + " (" + task.Name + ") asignada a " + machine.Name, farmId, machine.Id, null, task.FieldId, task.Type);
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

        /// <summary>Texto para el panel: qué está esperando una máquina.</summary>
        private string DescribeWhatMachineWaitsFor(MachineRuntime machine)
        {
            foreach (FarmTask task in tasks)
            {
                if (task.Status == AgroTaskStatus.Waiting && MachineMath.MachineFor(task.Type) == machine.Type)
                {
                    return "esperando " + task.FieldId + " (" + task.BlockReason + ")";
                }
            }
            return "sin tareas pendientes";
        }

        // ==================================================================
        // AVISOS DE LAS MÁQUINAS
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
                    machine.Name + " empezó " + task.Id + " (" + task.Name + ")", farmId, machine.Id, null, task.FieldId, task.Type);
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>TASK_COMPLETED: se actualiza el campo y se despierta a las máquinas que esperaban.</summary>
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
                        // Campo sembrado: empieza a crecer
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
                            field.GrowthBlockedBy = null; // el cultivo puede seguir creciendo
                        }
                        break;

                    case TaskType.Harvest:
                        field.TimesHarvested++;
                        field.HarvestedAt = DateTime.Now;
                        SetFieldStatus(field, FieldStatus.Harvested);
                        logger.Log(EventKind.FieldStateChanged, LogSeverity.Success,
                            field.Id + " recolectado (" + CropName(task.Crop) + ")", farmId, machine.Id, null, field.Id, task.Type);
                        break;
                }

                logger.Log(EventKind.TaskCompleted, LogSeverity.Success,
                    machine.Name + " completó " + task.Id + " (" + task.Name + ")", farmId, machine.Id, null, task.FieldId, task.Type);

                MoveToHistory(task);
                RefreshTaskStatuses();

                // ¡Clave! Ha cambiado el estado: despertar a todas las máquinas que esperaban.
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>TASK_ABORTED: la máquina deja la tarea a medias. Vuelve a PENDING conservando el progreso.</summary>
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
                    machine.Name + " interrumpió " + task.Id + " al " + task.Progress.ToString("0") + "% (" + reason + ")",
                    farmId, machine.Id, null, task.FieldId, task.Type);

                RefreshTaskStatuses();
                Monitor.PulseAll(sync); // otra máquina puede continuarla
            }
        }

        /// <summary>Si una máquina se desconecta, su tarea queda libre para otra.</summary>
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
                            task.Id + " liberada: " + machine.Name + " se ha desconectado", farmId, machine.Id, null, task.FieldId, task.Type);
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
        // CANCELAR (operador)
        // ==================================================================

        /// <summary>
        /// Cancela una tarea. Como las demás tareas del campo dependen unas de otras, se cancela
        /// el plan entero de ese campo. Devuelve el error o null. machineToNotify: máquina que la estaba haciendo.
        /// </summary>
        public string CancelTask(string taskId, string username, out string machineToNotify)
        {
            machineToNotify = null;
            lock (sync)
            {
                FarmTask task = FindTask(taskId);
                if (task == null)
                {
                    return "La tarea " + taskId + " no existe en esta explotación";
                }
                if (task.IsFinished())
                {
                    return "La tarea ya está terminada";
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
                    other.CancelReason = "Cancelada por " + username;
                    other.CompletedAt = DateTime.Now;
                    MoveToHistory(other);
                    logger.Log(EventKind.TaskCancelled, LogSeverity.Warning,
                        username + " canceló " + other.Id + " (" + other.Name + ")", farmId, other.AssignedMachineId, username, other.FieldId, other.Type);
                }

                // El campo queda libre
                field.PlannedCrop = null;
                field.AutoReplant = false;
                ResetField(field);

                RefreshTaskStatuses();
                Monitor.PulseAll(sync);
                return null;
            }
        }

        // ==================================================================
        // CRECIMIENTO (lo llama el hilo GrowthService cada medio segundo)
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

                    // Un campo recolectado queda libre al cabo de unos segundos
                    if (field.Status == FieldStatus.Harvested && field.HarvestedAt.HasValue &&
                        (DateTime.Now - field.HarvestedAt.Value).TotalSeconds >= config.HarvestedResetSeconds)
                    {
                        CropType? replant = field.AutoReplant ? field.PlannedCrop : null;
                        ResetField(field);
                        if (replant.HasValue)
                        {
                            CreateTasksForField(field, replant.Value); // replantar automáticamente
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
                    Monitor.PulseAll(sync); // p. ej. un cultivo ha madurado: despertar a las recolectoras
                }
            }
        }

        /// <summary>Hace crecer un campo. Devuelve true si ha cambiado algo importante.</summary>
        private bool Grow(Field field, double seconds)
        {
            CropDefinition definition = GetCrop(field.Crop.Value);
            double increment = seconds / (definition.GrowthMinutes * 60.0) * 100.0;
            double newGrowth = field.Growth + increment;

            // ¿Hay algún cuidado pendiente antes de este punto? Entonces el crecimiento se para ahí.
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
                    string need = blocking.Task == TaskType.Irrigate ? "riego" : "fumigación";
                    logger.Log(EventKind.FieldStateChanged, LogSeverity.Info,
                        field.Id + " necesita " + need + " (" + newGrowth.ToString("0") + "%)", farmId, null, null, field.Id, blocking.Task);
                    changed = true;
                }
            }
            field.Growth = Math.Min(100, newGrowth);

            FarmTask growthTask = FindTaskOfField(field.Id, TaskType.Growth);
            if (growthTask != null)
            {
                growthTask.Progress = field.Growth;
                growthTask.BlockReason = field.GrowthBlockedBy.HasValue ? "Necesita " + field.GrowthBlockedBy.Value.ToString().ToUpper() : null;
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
                    field.Id + " listo para recolectar (" + CropName(field.Crop.Value) + ")", farmId, null, null, field.Id, TaskType.Growth);
                changed = true;
            }
            return changed;
        }

        // ==================================================================
        // CONSULTAS (copias para enviar al centro de control)
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

        /// <summary>Tareas activas + las últimas terminadas.</summary>
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

        /// <summary>Tareas terminadas en un periodo (para el informe PDF).</summary>
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
        // AYUDAS
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
            // por si la configuración no lo tiene: valores por defecto
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
