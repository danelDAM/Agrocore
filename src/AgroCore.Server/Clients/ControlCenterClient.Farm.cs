using System;
using System.Collections.Generic;
using System.Net;
using AgroCore.Server.Machines;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;
using AgroCore.Shared.Security;

namespace AgroCore.Server.Clients
{
    /// <summary>
    /// Parte del ControlCenterClient con los mensajes de la explotación:
    /// estado, órdenes a máquinas, planes de cultivo, tareas, estadísticas e informes.
    ///
    /// AISLAMIENTO DE DATOS: antes de hacer nada se comprueba que la máquina, el campo o la tarea
    /// pertenecen a la explotación del usuario. Ejemplo:
    ///
    ///   Juan (FarmId 1) -> STOP F2-Harvester-01
    ///   ¿Existe la máquina?            sí
    ///   ¿Pertenece a la explotación 1? NO  -> DENEGADO (y se registra en el log de seguridad)
    /// </summary>
    public partial class ControlCenterClient
    {
        /// <summary>Devuelve true si el mensaje era de esta parte y se ha atendido.</summary>
        private bool HandleFarmMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.SelectFarm:
                    HandleSelectFarm(MessageSerializer.FromJson<SelectFarmRequest>(json));
                    return true;

                case MessageTypes.UdpSubscribe:
                    HandleUdpSubscribe(MessageSerializer.FromJson<UdpSubscribeRequest>(json));
                    return true;

                case MessageTypes.Start:
                case MessageTypes.Stop:
                case MessageTypes.Resume:
                case MessageTypes.ReturnToBase:
                    HandleMachineCommand(MessageSerializer.FromJson<MachineCommand>(json));
                    return true;

                case MessageTypes.StopAll:
                case MessageTypes.ResumeAll:
                case MessageTypes.ReturnAllToBase:
                case MessageTypes.EmergencyStop:
                    HandleFarmCommand(MessageSerializer.FromJson<FarmCommand>(json));
                    return true;

                case MessageTypes.CreateCropPlan:
                    HandleCropPlan(MessageSerializer.FromJson<CropPlanRequest>(json));
                    return true;

                case MessageTypes.CancelTask:
                    HandleCancelTask(MessageSerializer.FromJson<CancelTaskRequest>(json));
                    return true;

                case MessageTypes.StatisticsRequest:
                    HandleStatistics(MessageSerializer.FromJson<StatisticsRequest>(json));
                    return true;

                case MessageTypes.GlobalStatisticsRequest:
                    if (Require(Permission.ViewGlobalStatistics, type))
                    {
                        GlobalStatisticsResponse response = new GlobalStatisticsResponse();
                        response.Farms = context.Statistics.BuildGlobal(DateTime.MinValue, DateTime.MaxValue);
                        connection.Send(response);
                    }
                    return true;

                case MessageTypes.ReportRequest:
                    HandleReport(MessageSerializer.FromJson<ReportRequest>(json));
                    return true;

                default:
                    return false;
            }
        }

        // ------------------------------------------------------------------

        private void HandleSelectFarm(SelectFarmRequest request)
        {
            if (!Require(Permission.ViewAllFarms, request.Type))
            {
                return;
            }
            FarmRuntime farm = context.Farms.Get(request.FarmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(request.Type, "La explotación no existe"));
                return;
            }
            session.ViewFarmId = farm.Id;
            connection.Send(OperationResult.Ok(request.Type, "Viendo " + farm.Farm.Name));
            SendStateNow();
        }

        private void HandleUdpSubscribe(UdpSubscribeRequest request)
        {
            if (!Require(Permission.ViewFarm, request.Type))
            {
                return;
            }
            // La telemetría se envía a la misma IP desde la que se conectó por TCP
            IPAddress address = connection.RemoteAddress ?? IPAddress.Loopback;
            session.UdpEndPoint = new IPEndPoint(address, request.UdpPort);
            connection.Send(OperationResult.Ok(request.Type, "Telemetría UDP activada"));
            SendStateNow();
        }

        /// <summary>Envía el estado enseguida (sin esperar al siguiente segundo).</summary>
        private void SendStateNow()
        {
            FarmRuntime farm = context.Farms.Get(session.ViewFarmId);
            if (farm != null)
            {
                connection.TrySend(context.StateBuilder.Build(farm));
            }
        }

        // ------------------------------------------------------------------
        // ÓRDENES
        // ------------------------------------------------------------------

        private void HandleMachineCommand(MachineCommand command)
        {
            if (!Require(Permission.ControlMachines, command.Type))
            {
                return;
            }

            // ¿Existe la máquina?
            MachineRuntime machine = context.Farms.FindMachine(command.MachineId);
            if (machine == null)
            {
                connection.Send(OperationResult.Fail(command.Type, "La máquina " + command.MachineId + " no existe"));
                return;
            }

            // ¿Pertenece a la explotación del usuario?
            if (!session.IsAdmin && machine.FarmId != session.FarmId)
            {
                DenyOtherFarm(command.Type, "la máquina " + command.MachineId);
                return;
            }

            FarmRuntime farm = context.Farms.Get(machine.FarmId);
            string error = context.Commands.Execute(farm, machine, command.Type, session.Username);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(command.Type, error));
            }
            else
            {
                connection.Send(OperationResult.Ok(command.Type, command.Type + " enviado a " + machine.Name));
            }
        }

        private void HandleFarmCommand(FarmCommand command)
        {
            Permission permission = command.Type == MessageTypes.EmergencyStop ? Permission.EmergencyStop : Permission.GlobalCommands;
            if (!Require(permission, command.Type))
            {
                return;
            }

            int farmId;
            if (!ResolveFarm(command.FarmId, command.Type, out farmId))
            {
                return;
            }
            FarmRuntime farm = context.Farms.Get(farmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(command.Type, "La explotación no existe"));
                return;
            }

            string error = context.Commands.ExecuteForFarm(farm, command.Type, session.Username);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(command.Type, error));
            }
            else
            {
                connection.Send(OperationResult.Ok(command.Type, command.Type + " ejecutado en " + farm.Farm.Name));
            }
        }

        // ------------------------------------------------------------------
        // PLANES Y TAREAS
        // ------------------------------------------------------------------

        private void HandleCropPlan(CropPlanRequest request)
        {
            if (!Require(Permission.ManageCropPlans, request.Type))
            {
                return;
            }
            // El campo se busca SOLO en la explotación que está viendo el usuario (la suya si no es admin)
            FarmRuntime farm = context.Farms.Get(session.IsAdmin ? session.ViewFarmId : session.FarmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(request.Type, "Explotación no encontrada"));
                return;
            }

            string error = farm.Tasks.CreateCropPlan(request.FieldId, request.Crop, request.AutoReplant, session.Username);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(request.Type, error));
                return;
            }
            connection.Send(OperationResult.Ok(request.Type, "Plan creado: " + request.Crop + " en " + request.FieldId));
            SendStateNow();
        }

        private void HandleCancelTask(CancelTaskRequest request)
        {
            if (!Require(Permission.ManageTasks, request.Type))
            {
                return;
            }
            FarmRuntime farm = context.Farms.Get(session.IsAdmin ? session.ViewFarmId : session.FarmId);
            if (farm == null || !farm.Tasks.HasTask(request.TaskId))
            {
                // No se dice si existe en otra explotación: simplemente no es suya
                if (!session.IsAdmin)
                {
                    DenyOtherFarm(request.Type, "la tarea " + request.TaskId);
                }
                else
                {
                    connection.Send(OperationResult.Fail(request.Type, "La tarea no existe en esta explotación"));
                }
                return;
            }

            string machineToNotify;
            string error = farm.Tasks.CancelTask(request.TaskId, session.Username, out machineToNotify);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(request.Type, error));
                return;
            }

            // Si una máquina la estaba haciendo, se le avisa para que la abandone
            if (machineToNotify != null)
            {
                MachineRuntime machine = context.Farms.FindMachine(machineToNotify);
                if (machine != null && machine.Connection != null)
                {
                    TaskCancelledNotice notice = new TaskCancelledNotice();
                    notice.TaskId = machine.CurrentTaskId ?? request.TaskId;
                    notice.Reason = "Cancelada por " + session.Username;
                    machine.Connection.TrySend(notice);
                    machine.SetCurrentTask(null, null, null);
                }
            }
            connection.Send(OperationResult.Ok(request.Type, "Plan del campo cancelado"));
            SendStateNow();
        }

        // ------------------------------------------------------------------
        // ESTADÍSTICAS E INFORMES
        // ------------------------------------------------------------------

        private void HandleStatistics(StatisticsRequest request)
        {
            if (!Require(Permission.ViewStatistics, request.Type))
            {
                return;
            }
            int farmId;
            if (!ResolveFarm(request.FarmId, request.Type, out farmId))
            {
                return;
            }
            FarmRuntime farm = context.Farms.Get(farmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(request.Type, "Explotación no encontrada"));
                return;
            }
            StatisticsResponse response = new StatisticsResponse();
            response.Statistics = context.Statistics.Build(farm, DateTime.MinValue, DateTime.MaxValue);
            connection.Send(response);
        }

        /// <summary>
        /// Datos del informe PDF. Un agricultor solo puede pedir el de SU explotación:
        /// nunca se generan por defecto datos de otras explotaciones.
        /// </summary>
        private void HandleReport(ReportRequest request)
        {
            if (!Require(Permission.GenerateReports, request.Type))
            {
                return;
            }
            int farmId;
            if (!ResolveFarm(request.FarmId, request.Type, out farmId))
            {
                return;
            }
            FarmRuntime farm = context.Farms.Get(farmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(request.Type, "Explotación no encontrada"));
                return;
            }

            DateTime from = request.From.Date;
            DateTime to = request.To.Date.AddDays(1).AddTicks(-1);

            ReportData report = new ReportData();
            report.GeneratedAt = DateTime.Now;
            report.GeneratedBy = session.FullName;
            report.From = from;
            report.To = to;
            report.FarmId = farm.Id;
            report.FarmName = farm.Farm.Name;
            report.FarmerName = farm.OwnerName;

            AgroCore.Shared.Messages.FarmStateMessage state = context.StateBuilder.Build(farm);
            report.Summary = state.Summary;
            report.Machines = state.Machines;
            report.Fields = state.Fields;
            report.Statistics = context.Statistics.Build(farm, from, to);
            report.Tasks = farm.Tasks.GetFinishedTasks(from, to);

            // Incidencias: avisos, errores y eventos de seguridad del periodo
            List<LogEntry> incidents = new List<LogEntry>();
            foreach (LogEntry entry in context.Logger.GetEntries(farm.Id, from, to))
            {
                if (entry.Severity == LogSeverity.Warning || entry.Severity == LogSeverity.Error || entry.Severity == LogSeverity.Security)
                {
                    incidents.Add(entry);
                }
            }
            if (incidents.Count > 150)
            {
                incidents = incidents.GetRange(incidents.Count - 150, 150);
            }
            report.Incidents = incidents;

            ReportResponse response = new ReportResponse();
            response.Report = report;
            connection.Send(response);

            context.Logger.Log(EventKind.Info, LogSeverity.Info,
                session.FullName + " generó el informe de " + farm.Farm.Name + " (" + from.ToShortDateString() + " - " + to.ToShortDateString() + ")",
                farm.Id, null, session.Username, null, null);
        }
    }
}
