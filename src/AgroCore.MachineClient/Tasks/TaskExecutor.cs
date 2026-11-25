using System;
using System.Threading;
using AgroCore.MachineClient.Battery;
using AgroCore.MachineClient.Machine;
using AgroCore.MachineClient.Networking;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.MachineClient.Tasks
{
    /// <summary>
    /// Ejecuta una tarea asignada por el servidor:
    ///   1. Ir hasta el campo.
    ///   2. Avisar TASK_STARTED.
    ///   3. Recorrer el campo en pasadas de ida y vuelta (como un tractor real).
    ///   4. Tras cada pasada, comprobar batería, cancelación y orden de volver.
    ///   5. Avisar TASK_COMPLETED (o TASK_ABORTED si se interrumpe).
    /// </summary>
    public class TaskExecutor
    {
        private readonly string machineId;
        private readonly MachineType type;
        private readonly MachineProfile profile;
        private readonly MachineState state;
        private readonly MachineControl control;
        private readonly BatteryManager battery;
        private readonly Navigator navigator;
        private readonly ServerLink link;
        private readonly FarmLayout layout;
        private readonly Action<string> log;

        public TaskExecutor(string machineId, MachineType type, MachineProfile profile, MachineState state, MachineControl control,
                            BatteryManager battery, Navigator navigator, ServerLink link, FarmLayout layout, Action<string> log)
        {
            this.machineId = machineId;
            this.type = type;
            this.profile = profile;
            this.state = state;
            this.control = control;
            this.battery = battery;
            this.navigator = navigator;
            this.link = link;
            this.layout = layout;
            this.log = log;
        }

        public void Execute(TaskAssignment task)
        {
            log("Tarea " + task.TaskId + ": " + task.TaskType + " en " + task.FieldId + " (progreso inicial " + task.Progress.ToString("0") + "%)");
            state.TaskProgress = task.Progress;

            // ---- 1. Ir al campo ----
            state.Set(MachineStatus.MovingToField, "hacia " + task.FieldId);
            bool arrived = navigator.GoToField(task.FieldIndex, () => GetAbortReason(task) != null);
            if (!arrived)
            {
                Abort(task, GetAbortReason(task));
                return;
            }

            // ---- 2. Empezar ----
            TaskStarted started = new TaskStarted();
            started.MachineId = machineId;
            started.TaskId = task.TaskId;
            started.FieldId = task.FieldId;
            link.Send(started);
            state.Set(MachineStatus.Working, WorkName(task.TaskType) + " " + task.FieldId);

            // ---- 3. Pasadas sobre el campo ----
            int lanes = MachineMath.LaneCount(profile, layout.FieldSize);
            double laneWidth = layout.FieldSize / lanes;
            double minX = layout.FieldMinX(task.FieldIndex);
            double minZ = layout.FieldMinZ(task.FieldIndex);
            double y = navigator.FieldWorkAltitude;
            int firstLane = (int)Math.Floor(task.Progress / 100.0 * lanes);
            double load = 0;
            double yieldPerLane = HarvestYield(task.Crop) / lanes;

            for (int lane = firstLane; lane < lanes; lane++)
            {
                double z = minZ + laneWidth * (lane + 0.5);
                double left = minX + 0.8;
                double right = minX + layout.FieldSize - 0.8;
                bool leftToRight = lane % 2 == 0;

                navigator.MoveTo(new Position(leftToRight ? left : right, y, z), true);
                navigator.MoveTo(new Position(leftToRight ? right : left, y, z), true);

                double progress = (lane + 1) * 100.0 / lanes;
                state.TaskProgress = progress;

                // Recolectoras: si la tolva se llena, descargar (pausa)
                if (task.TaskType == TaskType.Harvest)
                {
                    load += yieldPerLane;
                    if (load >= profile.Capacity && lane < lanes - 1)
                    {
                        state.Set(MachineStatus.Working, "descargando tolva (" + profile.Capacity.ToString("0") + " kg)");
                        Thread.Sleep(2000);
                        load = 0;
                        state.Set(MachineStatus.Working, WorkName(task.TaskType) + " " + task.FieldId);
                    }
                }

                if (lane < lanes - 1)
                {
                    string reason = GetAbortReason(task);
                    if (reason != null)
                    {
                        Abort(task, reason);
                        return;
                    }
                }
            }

            // ---- 5. Terminada ----
            TaskCompleted completed = new TaskCompleted();
            completed.MachineId = machineId;
            completed.TaskId = task.TaskId;
            completed.FieldId = task.FieldId;
            link.Send(completed);
            state.TaskProgress = 0;
            state.Set(MachineStatus.Idle, null);
            log("Tarea " + task.TaskId + " completada");
        }

        /// <summary>
        /// ¿Hay que dejar la tarea? Devuelve el motivo, o null si se puede seguir.
        /// Batería: primero mira si puede terminar y volver; si no, la deja a medias para volver a tiempo.
        /// </summary>
        private string GetAbortReason(TaskAssignment task)
        {
            if (control.IsTaskCancelled(task.TaskId))
            {
                return "CANCELADA";
            }
            if (control.ReturnRequested)
            {
                return "RETURN_TO_BASE";
            }
            Position position = state.GetPosition();
            if (!battery.CanFinishAndReturn(position, state.TaskProgress))
            {
                return "BATERÍA BAJA";
            }
            return null;
        }

        private void Abort(TaskAssignment task, string reason)
        {
            if (reason == null)
            {
                reason = "desconocido";
            }
            log("Tarea " + task.TaskId + " interrumpida al " + state.TaskProgress.ToString("0") + "%: " + reason);

            // Si la canceló el operador el servidor ya lo sabe; si no, hay que avisarle
            if (!control.IsTaskCancelled(task.TaskId))
            {
                TaskAborted aborted = new TaskAborted();
                aborted.MachineId = machineId;
                aborted.TaskId = task.TaskId;
                aborted.FieldId = task.FieldId;
                aborted.Progress = state.TaskProgress;
                aborted.Reason = reason;
                link.Send(aborted);
            }
            state.TaskProgress = 0;
            state.Set(MachineStatus.Idle, null);
        }

        private static string WorkName(TaskType taskType)
        {
            switch (taskType)
            {
                case TaskType.Sow: return "sembrando";
                case TaskType.Irrigate: return "regando";
                case TaskType.Spray: return "fumigando";
                case TaskType.Harvest: return "recolectando";
                default: return "trabajando";
            }
        }

        private static double HarvestYield(CropType crop)
        {
            foreach (CropDefinition definition in CropDefinition.Defaults())
            {
                if (definition.Type == crop)
                {
                    return definition.Yield;
                }
            }
            return 150;
        }
    }
}
