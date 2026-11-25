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
    /// Zerbitzariak esleitutako zeregin bat exekutatzen du:
    ///   1. Sororaino joan.
    ///   2. TASK_STARTED abisatu.
    ///   3. Soroa joan-etorriko pasaldietan zeharkatu (benetako traktore batek bezala).
    ///   4. Pasaldi bakoitzaren ondoren, bateria, bertan behera uztea eta itzultzeko agindua egiaztatu.
    ///   5. TASK_COMPLETED abisatu (edo TASK_ABORTED, eteten bada).
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
            log(task.TaskId + " zeregina: " + task.TaskType + " " + task.FieldId + " soroan (hasierako aurrerapena: %" + task.Progress.ToString("0") + ")");
            state.TaskProgress = task.Progress;

            // ---- 1. Sorora joan ----
            state.Set(MachineStatus.MovingToField, task.FieldId + " sororantz");
            bool arrived = navigator.GoToField(task.FieldIndex, () => GetAbortReason(task) != null);
            if (!arrived)
            {
                Abort(task, GetAbortReason(task));
                return;
            }

            // ---- 2. Hasi ----
            TaskStarted started = new TaskStarted();
            started.MachineId = machineId;
            started.TaskId = task.TaskId;
            started.FieldId = task.FieldId;
            link.Send(started);
            state.Set(MachineStatus.Working, WorkName(task.TaskType) + " " + task.FieldId);

            // ---- 3. Pasaldiak soroan ----
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

                // Uzta-biltzaileak: tolba betetzen bada, hustu (etenaldia)
                if (task.TaskType == TaskType.Harvest)
                {
                    load += yieldPerLane;
                    if (load >= profile.Capacity && lane < lanes - 1)
                    {
                        state.Set(MachineStatus.Working, "tolba husten (" + profile.Capacity.ToString("0") + " kg)");
                        Thread.Sleep(2000 / SimulationClock.Speed);
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

            // ---- 5. Amaituta ----
            TaskCompleted completed = new TaskCompleted();
            completed.MachineId = machineId;
            completed.TaskId = task.TaskId;
            completed.FieldId = task.FieldId;
            link.Send(completed);
            state.TaskProgress = 0;
            state.Set(MachineStatus.Idle, null);
            log(task.TaskId + " zeregina osatuta");
        }

        /// <summary>
        /// Zeregina utzi behar da? Arrazoia itzultzen du, edo null jarraitu badaiteke.
        /// Bateria: lehenik begiratzen du amaitu eta itzul daitekeen; bestela, erdizka uzten du garaiz itzultzeko.
        /// </summary>
        private string GetAbortReason(TaskAssignment task)
        {
            if (control.IsTaskCancelled(task.TaskId))
            {
                return "BERTAN BEHERA UTZITA";
            }
            if (control.ReturnRequested)
            {
                return "RETURN_TO_BASE";
            }
            Position position = state.GetPosition();
            if (!battery.CanFinishAndReturn(position, state.TaskProgress))
            {
                return "BATERIA BAXUA";
            }
            return null;
        }

        private void Abort(TaskAssignment task, string reason)
        {
            if (reason == null)
            {
                reason = "ezezaguna";
            }
            log(task.TaskId + " zeregina %" + state.TaskProgress.ToString("0") + "an etenda: " + reason);

            // Operadoreak bertan behera utzi badu, zerbitzariak badaki; bestela, abisatu egin behar zaio
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
                case TaskType.Sow: return "ereiten";
                case TaskType.Irrigate: return "ureztatzen";
                case TaskType.Spray: return "fumigatzen";
                case TaskType.Harvest: return "uzta biltzen";
                default: return "lanean";
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
