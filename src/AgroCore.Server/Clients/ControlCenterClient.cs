using System;
using AgroCore.Server.Machines;
using AgroCore.Server.Security;
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
    /// WPF Kontrol Zentro BAT artatzen du. Bezero horren hari propioan exekutatzen da.
    ///
    /// Funtzionamendua:
    ///   1. LOGIN_REQUEST zuzen baten zain geratzen da (pasahitza oker jartzen badu, berriro saia daiteke).
    ///   2. Saioa sortzen du.
    ///   3. Mezuak begizta batean irakurtzen ditu eta "type"-aren arabera switch batekin artatzen ditu.
    ///   4. Saioa ixtean edo deskonektatzean, saioa ezabatzen du.
    ///
    /// SEGURTASUNA: eskaera bakoitza hemen egiaztatzen da, zerbitzarian:
    ///   - Erabiltzailearen rolak baimena al du honetarako?          -> Require(...)
    ///   - Baliabidea BERE ustiategikoa al da?                       -> ResolveFarm(...)
    /// Ez gara fidatzen WPFak botoiak ezkutatu izanaz.
    /// </summary>
    public partial class ControlCenterClient
    {
        private readonly ServerContext context;
        private readonly Connection connection;
        private UserSession session;

        public ControlCenterClient(ServerContext context, Connection connection)
        {
            this.context = context;
            this.connection = connection;
        }

        public void Run(string firstLine)
        {
            context.Logger.Log(EventKind.ClientConnected, LogSeverity.Info,
                "Kontrol Zentroa konektatuta hemendik: " + connection.RemoteText);

            try
            {
                // ---- 1. Saioa hasi ----
                string line = firstLine;
                while (session == null)
                {
                    if (line == null)
                    {
                        return; // saioa hasi gabe deskonektatu da
                    }

                    if (MessageSerializer.ReadType(line) == MessageTypes.LoginRequest)
                    {
                        LoginRequest request = MessageSerializer.FromJson<LoginRequest>(line);
                        HandleLogin(request);
                    }
                    else
                    {
                        connection.Send(OperationResult.Fail(MessageSerializer.ReadType(line), "Lehenik saioa hasi behar duzu"));
                    }

                    if (session == null)
                    {
                        line = connection.ReadLine();
                    }
                }

                // ---- 2. Mezuen begizta ----
                while (true)
                {
                    line = connection.ReadLine();
                    if (line == null)
                    {
                        break; // konexioa itxita
                    }

                    string type = MessageSerializer.ReadType(line);
                    if (type == MessageTypes.LogoutRequest)
                    {
                        connection.TrySend(OperationResult.Ok(type, "Saioa itxita"));
                        context.Logger.Log(EventKind.Logout, LogSeverity.Info,
                            session.FullName + " saioa itxi du", session.FarmId, null, session.Username, null, null);
                        break;
                    }

                    try
                    {
                        HandleMessage(type, line);
                    }
                    catch (Exception ex)
                    {
                        connection.TrySend(OperationResult.Fail(type, "Errorea zerbitzarian: " + ex.Message));
                        context.Logger.Log(EventKind.Error, LogSeverity.Error,
                            "Errorea " + type + " artatzean (" + session.Username + "): " + ex.Message);
                    }
                }
            }
            finally
            {
                if (session != null)
                {
                    context.Sessions.Remove(session.Token);
                }
                context.Logger.Log(EventKind.ClientDisconnected, LogSeverity.Info,
                    "Kontrol Zentroa deskonektatuta (" + connection.RemoteText + ")");
            }
        }

        // ==================================================================
        // LOGIN
        // ==================================================================

        private void HandleLogin(LoginRequest request)
        {
            LoginResult result = context.Auth.Login(request.Username, request.Password, connection.RemoteText);

            LoginResponse response = new LoginResponse();
            if (!result.Success)
            {
                response.Success = false;
                response.Message = result.Message;
                connection.Send(response);
                return;
            }

            User user = result.User;

            // Saioa zerbitzarian sortu
            UserSession newSession = new UserSession();
            newSession.Token = PasswordHasher.GenerateToken();
            newSession.UserId = user.Id;
            newSession.Username = user.Username;
            newSession.FullName = user.FullName;
            newSession.Role = user.Role;
            newSession.FarmId = user.FarmId;
            newSession.Connection = connection;
            if (result.Farm != null)
            {
                newSession.FarmName = result.Farm.Name;
                newSession.ViewFarmId = result.Farm.Id;
            }
            else
            {
                // Administratzailea: dagoen lehen ustiategia ikusten hasten da
                newSession.FarmName = "Ustiategi guztiak";
                if (context.Store.GetFarms().Count > 0)
                {
                    newSession.ViewFarmId = context.Store.GetFarms()[0].Id;
                }
            }
            context.Sessions.Add(newSession);
            session = newSession;

            response.Success = true;
            response.Message = "Ongi etorri, " + user.FullName;
            response.UserId = user.Id;
            response.Username = user.Username;
            response.FullName = user.FullName;
            response.Role = user.Role;
            response.FarmId = user.FarmId;
            response.FarmName = newSession.FarmName;
            response.SessionToken = newSession.Token;
            connection.Send(response);

            context.Logger.Log(EventKind.LoginSuccess, LogSeverity.Success,
                user.FullName + " (" + RolePermissions.DisplayName(user.Role) + ") saioa hasi du hemendik: " + connection.RemoteText,
                user.FarmId == 0 ? (int?)null : user.FarmId, null, user.Username, null, null);
        }

        // ==================================================================
        // SAIOA HASI ONDOKO MEZUAK
        // ==================================================================

        private void HandleMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.ChangePasswordRequest:
                    HandleChangePassword(MessageSerializer.FromJson<ChangePasswordRequest>(json));
                    break;

                // ----- Administrazioa -----
                case MessageTypes.ListUsers:
                    if (Require(Permission.ManageUsers, type))
                    {
                        ListUsersResponse users = context.Users.ListUsers();
                        // Une honetan konektatuta dauden makinak (ustiategi bakoitzaren zuzeneko datua)
                        foreach (FarmDto farmDto in users.Farms)
                        {
                            FarmRuntime runtime = context.Farms == null ? null : context.Farms.Get(farmDto.Id);
                            if (runtime != null)
                            {
                                foreach (MachineRuntime machine in runtime.GetMachines())
                                {
                                    if (machine.Online)
                                    {
                                        farmDto.OnlineMachines++;
                                    }
                                }
                            }
                        }
                        connection.Send(users);
                    }
                    break;

                case MessageTypes.CreateFarmer:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.CreateFarmer(MessageSerializer.FromJson<CreateFarmerRequest>(json), session.Username));
                    }
                    break;

                case MessageTypes.CreateUser:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.CreateUser(MessageSerializer.FromJson<CreateUserRequest>(json), session.Username));
                    }
                    break;

                case MessageTypes.UpdateUser:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.UpdateUser(MessageSerializer.FromJson<UpdateUserRequest>(json), session.Username));
                    }
                    break;

                case MessageTypes.SetUserActive:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.SetActive(MessageSerializer.FromJson<SetUserActiveRequest>(json), session.UserId, session.Username));
                    }
                    break;

                case MessageTypes.DeleteUser:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.DeleteUser(MessageSerializer.FromJson<DeleteUserRequest>(json), session.UserId, session.Username));
                    }
                    break;

                case MessageTypes.RegenerateFleetKey:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.RegenerateFleetKey(MessageSerializer.FromJson<RegenerateFleetKeyRequest>(json), session.Username));
                    }
                    break;

                case MessageTypes.GetConfig:
                    if (Require(Permission.ConfigureSystem, type))
                    {
                        ConfigResponse configResponse = new ConfigResponse();
                        configResponse.Config = context.Config.Current;
                        connection.Send(configResponse);
                    }
                    break;

                case MessageTypes.UpdateConfig:
                    if (Require(Permission.ConfigureSystem, type))
                    {
                        UpdateConfigRequest update = MessageSerializer.FromJson<UpdateConfigRequest>(json);
                        context.Config.Update(update.Config);
                        context.Logger.Log(EventKind.ConfigChanged, LogSeverity.Info,
                            session.Username + " erabiltzaileak sistemaren konfigurazioa aldatu du");
                        connection.Send(OperationResult.Ok(type, "Konfigurazioa gordeta"));
                    }
                    break;

                // ----- Gertaeren erregistroa -----
                case MessageTypes.LogsRequest:
                    if (Require(Permission.ViewLogs, type))
                    {
                        LogsRequest logsRequest = MessageSerializer.FromJson<LogsRequest>(json);
                        int farmId;
                        if (ResolveFarm(logsRequest.FarmId, type, out farmId))
                        {
                            LogsResponse logs = new LogsResponse();
                            logs.Entries = context.Logger.GetRecent(farmId, Math.Clamp(logsRequest.Count, 1, 1000), false);
                            connection.Send(logs);
                        }
                    }
                    break;

                default:
                    if (!HandleFarmMessage(type, json))
                    {
                        connection.Send(OperationResult.Fail(type, "Mezu ezezaguna: " + type));
                    }
                    break;
            }
        }

        private void HandleChangePassword(ChangePasswordRequest request)
        {
            if (!Require(Permission.ChangeOwnPassword, request.Type))
            {
                return;
            }

            string error = context.Auth.ChangePassword(session.UserId, request.CurrentPassword, request.NewPassword);
            if (error != null)
            {
                connection.Send(OperationResult.Fail(request.Type, error));
                context.Logger.Log(EventKind.Info, LogSeverity.Warning,
                    session.Username + " erabiltzailea pasahitza aldatzen saiatu da: " + error, session.FarmId, null, session.Username, null, null);
                return;
            }

            connection.Send(OperationResult.Ok(request.Type, "Pasahitza ondo aldatu da"));
            context.Logger.Log(EventKind.PasswordChanged, LogSeverity.Info,
                session.Username + " erabiltzaileak bere pasahitza aldatu du", session.FarmId, null, session.Username, null, null);
        }

        // ==================================================================
        // SEGURTASUN-EGIAZTAPENAK
        // ==================================================================

        /// <summary>Erabiltzailearen rolak baimena duela egiaztatzen du. Bestela, errorearekin erantzun eta erregistratu egiten du.</summary>
        private bool Require(Permission permission, string requestType)
        {
            if (RolePermissions.Has(session.Role, permission))
            {
                return true;
            }

            connection.Send(OperationResult.Fail(requestType,
                "Sarbidea ukatuta: zure rolak (" + RolePermissions.DisplayName(session.Role) + ") ez du ekintza hau baimentzen"));
            context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                "UKATUTA: " + session.Username + " (" + session.Role + ") " + requestType + " egiten saiatu da",
                session.FarmId, null, session.Username, null, null);
            return false;
        }

        /// <summary>
        /// Zein ustiategitan lan egiten den erabakitzen du (DATUEN ISOLAMENDUA).
        ///  - Erabiltzaile arrunta: BETI bere ustiategia. Beste bat eskatzen badu, ukatu egiten da.
        ///  - Administratzailea: eskatzen duena, edo bat ere adierazten ez badu, ikusten ari dena.
        /// </summary>
        private bool ResolveFarm(int requestedFarmId, string requestType, out int farmId)
        {
            if (session.IsAdmin)
            {
                farmId = requestedFarmId > 0 ? requestedFarmId : session.ViewFarmId;
                return true;
            }

            farmId = session.FarmId;
            if (requestedFarmId > 0 && requestedFarmId != session.FarmId)
            {
                DenyOtherFarm(requestType, requestedFarmId + ". ustiategia");
                return false;
            }
            return true;
        }

        private void DenyOtherFarm(string requestType, string what)
        {
            connection.Send(OperationResult.Fail(requestType, "Sarbidea ukatuta: " + what + " ez da zure ustiategikoa"));
            context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                "UKATUTA: " + session.Username + " (" + session.FarmId + ". ustiategia) " + requestType + " egiten saiatu da hemen: " + what,
                session.FarmId, null, session.Username, null, null);
        }
    }
}
