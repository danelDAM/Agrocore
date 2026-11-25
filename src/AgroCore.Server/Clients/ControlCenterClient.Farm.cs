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
    /// ControlCenterClient-aren zatia, ustiategiaren mezuekin:
    /// egoera, makinentzako aginduak, labore-planak, zereginak, estatistikak eta txostenak.
    ///
    /// DATUEN ISOLAMENDUA: ezer egin aurretik, makina, soroa edo zeregina
    /// erabiltzailearen ustiategikoak direla egiaztatzen da. Adibidea:
    ///
    ///   Juan (FarmId 1) -> STOP F2-Harvester-01
    ///   Makina existitzen da?           bai
    ///   1. ustiategikoa da?             EZ  -> UKATUTA (eta segurtasun-erregistroan gordetzen da)
    /// </summary>
    public partial class ControlCenterClient
    {
        /// <summary>true itzultzen du mezua zati honetakoa bazen eta artatu bada.</summary>
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

                case MessageTypes.SetSimulationSpeed:
                    HandleSimulationSpeed(MessageSerializer.FromJson<SimulationSpeedMessage>(json));
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
                connection.Send(OperationResult.Fail(request.Type, "Ustiategia ez da existitzen"));
                return;
            }
            session.ViewFarmId = farm.Id;
            connection.Send(OperationResult.Ok(request.Type, "Ikusten: " + farm.Farm.Name));
            SendStateNow();
        }

        private void HandleUdpSubscribe(UdpSubscribeRequest request)
        {
            if (!Require(Permission.ViewFarm, request.Type))
            {
                return;
            }
            // Telemetria TCP bidez konektatu zen IP berera bidaltzen da
            IPAddress address = connection.RemoteAddress ?? IPAddress.Loopback;
            session.UdpEndPoint = new IPEndPoint(address, request.UdpPort);
            connection.Send(OperationResult.Ok(request.Type, "UDP telemetria aktibatuta"));
            SendStateNow();
        }

        /// <summary>Egoera berehala bidaltzen du (hurrengo segundoaren zain egon gabe).</summary>
        private void SendStateNow()
        {
            FarmRuntime farm = context.Farms.Get(session.ViewFarmId);
            if (farm != null)
            {
                connection.TrySend(context.StateBuilder.Build(farm));
            }
        }

        // ------------------------------------------------------------------
        // AGINDUAK
        // ------------------------------------------------------------------

        private void HandleMachineCommand(MachineCommand command)
        {
            if (!Require(Permission.ControlMachines, command.Type))
            {
                return;
            }

            // Makina existitzen da?
            MachineRuntime machine = context.Farms.FindMachine(command.MachineId);
            if (machine == null)
            {
                connection.Send(OperationResult.Fail(command.Type, command.MachineId + " makina ez da existitzen"));
                return;
            }

            // Erabiltzailearen ustiategikoa da?
            if (!session.IsAdmin && machine.FarmId != session.FarmId)
            {
                DenyOtherFarm(command.Type, command.MachineId + " makina");
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
                connection.Send(OperationResult.Ok(command.Type, command.Type + " bidalita: " + machine.Name));
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
                connection.Send(OperationResult.Fail(command.Type, "Ustiategia ez da existitzen"));
                return;
            }

            string error = context.Commands.ExecuteForFarm(farm, command.Type, session.Username);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(command.Type, error));
            }
            else
            {
                connection.Send(OperationResult.Ok(command.Type, command.Type + " exekutatuta: " + farm.Farm.Name));
            }
        }

        /// <summary>
        /// Simulazioaren abiadura aldatzen du (x1 - x10). Zerbitzariak ustiategian gordetzen du
        /// (laboreen hazkundea azkartzeko) eta konektatutako makina guztiei bidaltzen die.
        /// </summary>
        private void HandleSimulationSpeed(SimulationSpeedMessage request)
        {
            if (!Require(Permission.GlobalCommands, request.Type))
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
                connection.Send(OperationResult.Fail(request.Type, "Ustiategia ez da existitzen"));
                return;
            }
            if (request.Speed < SimulationSpeedMessage.MinSpeed || request.Speed > SimulationSpeedMessage.MaxSpeed)
            {
                connection.Send(OperationResult.Fail(request.Type, "Abiadura okerra: x1 eta x10 artean izan behar du"));
                return;
            }

            farm.SimulationSpeed = request.Speed;

            int count = 0;
            foreach (MachineRuntime machine in farm.GetMachines())
            {
                if (machine.Online && machine.Connection != null)
                {
                    SimulationSpeedMessage notice = new SimulationSpeedMessage();
                    notice.FarmId = farm.Id;
                    notice.Speed = request.Speed;
                    machine.Connection.TrySend(notice);
                    count++;
                }
            }

            context.Logger.Log(EventKind.ConfigChanged, LogSeverity.Info,
                session.Username + " erabiltzaileak simulazioaren abiadura x" + request.Speed + " jarri du (" + count + " makina)",
                farm.Id, null, session.Username, null, null);
            connection.Send(OperationResult.Ok(request.Type, "Simulazioaren abiadura: x" + request.Speed));
        }

        // ------------------------------------------------------------------
        // PLANAK ETA ZEREGINAK
        // ------------------------------------------------------------------

        private void HandleCropPlan(CropPlanRequest request)
        {
            if (!Require(Permission.ManageCropPlans, request.Type))
            {
                return;
            }
            // Soroa erabiltzailea ikusten ari den ustiategian BAKARRIK bilatzen da (berea, administratzailea ez bada)
            FarmRuntime farm = context.Farms.Get(session.IsAdmin ? session.ViewFarmId : session.FarmId);
            if (farm == null)
            {
                connection.Send(OperationResult.Fail(request.Type, "Ez da ustiategia aurkitu"));
                return;
            }

            string error = farm.Tasks.CreateCropPlan(request.FieldId, request.Crop, request.AutoReplant, session.Username);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(request.Type, error));
                return;
            }
            connection.Send(OperationResult.Ok(request.Type, "Plana sortuta: " + request.Crop + " (" + request.FieldId + ")"));
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
                // Ez da esaten beste ustiategi batean dagoen: besterik gabe, ez da berea
                if (!session.IsAdmin)
                {
                    DenyOtherFarm(request.Type, request.TaskId + " zeregina");
                }
                else
                {
                    connection.Send(OperationResult.Fail(request.Type, "Zeregina ez dago ustiategi honetan"));
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

            // Makina bat egiten ari bazen, abisatu egiten zaio utz dezan
            if (machineToNotify != null)
            {
                MachineRuntime machine = context.Farms.FindMachine(machineToNotify);
                if (machine != null && machine.Connection != null)
                {
                    TaskCancelledNotice notice = new TaskCancelledNotice();
                    notice.TaskId = machine.CurrentTaskId ?? request.TaskId;
                    notice.Reason = "Bertan behera utzita, egilea: " + session.Username;
                    machine.Connection.TrySend(notice);
                    machine.SetCurrentTask(null, null, null);
                }
            }
            connection.Send(OperationResult.Ok(request.Type, "Soroaren plana bertan behera utzita"));
            SendStateNow();
        }

        // ------------------------------------------------------------------
        // ESTATISTIKAK ETA TXOSTENAK
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
                connection.Send(OperationResult.Fail(request.Type, "Ez da ustiategia aurkitu"));
                return;
            }
            StatisticsResponse response = new StatisticsResponse();
            response.Statistics = context.Statistics.Build(farm, DateTime.MinValue, DateTime.MaxValue);
            connection.Send(response);
        }

        /// <summary>
        /// PDF txostenaren datuak. Nekazari batek BERE ustiategiarena bakarrik eska dezake:
        /// ez dira inoiz lehenespenez beste ustiategi batzuen datuak sortzen.
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
                connection.Send(OperationResult.Fail(request.Type, "Ez da ustiategia aurkitu"));
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

            // Gorabeherak: aldiko abisuak, erroreak eta segurtasun-gertaerak
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
                session.FullName + " erabiltzaileak " + farm.Farm.Name + " ustiategiaren txostena sortu du (" + from.ToShortDateString() + " - " + to.ToShortDateString() + ")",
                farm.Id, null, session.Username, null, null);
        }
    }
}
