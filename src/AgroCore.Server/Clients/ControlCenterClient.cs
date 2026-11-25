using System;
using AgroCore.Server.Security;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;
using AgroCore.Shared.Security;

namespace AgroCore.Server.Clients
{
    /// <summary>
    /// Atiende a UN centro de control WPF. Se ejecuta en el hilo propio de ese cliente.
    ///
    /// Funcionamiento:
    ///   1. Espera un LOGIN_REQUEST correcto (puede reintentar si se equivoca de contraseña).
    ///   2. Crea la sesión.
    ///   3. Lee mensajes en bucle y los atiende con un switch según su "type".
    ///   4. Al cerrar sesión o desconectarse, borra la sesión.
    ///
    /// SEGURIDAD: cada petición se comprueba aquí, en el servidor:
    ///   - ¿El rol del usuario tiene permiso para esto?            -> Require(...)
    ///   - ¿El recurso pertenece a SU explotación?                  -> ResolveFarm(...)
    /// No se confía en que el WPF haya escondido los botones.
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
                "Centro de control conectado desde " + connection.RemoteText);

            try
            {
                // ---- 1. Login ----
                string line = firstLine;
                while (session == null)
                {
                    if (line == null)
                    {
                        return; // se desconectó sin iniciar sesión
                    }

                    if (MessageSerializer.ReadType(line) == MessageTypes.LoginRequest)
                    {
                        LoginRequest request = MessageSerializer.FromJson<LoginRequest>(line);
                        HandleLogin(request);
                    }
                    else
                    {
                        connection.Send(OperationResult.Fail(MessageSerializer.ReadType(line), "Debe iniciar sesión primero"));
                    }

                    if (session == null)
                    {
                        line = connection.ReadLine();
                    }
                }

                // ---- 2. Bucle de mensajes ----
                while (true)
                {
                    line = connection.ReadLine();
                    if (line == null)
                    {
                        break; // conexión cerrada
                    }

                    string type = MessageSerializer.ReadType(line);
                    if (type == MessageTypes.LogoutRequest)
                    {
                        connection.TrySend(OperationResult.Ok(type, "Sesión cerrada"));
                        context.Logger.Log(EventKind.Logout, LogSeverity.Info,
                            session.FullName + " cerró sesión", session.FarmId, null, session.Username, null, null);
                        break;
                    }

                    try
                    {
                        HandleMessage(type, line);
                    }
                    catch (Exception ex)
                    {
                        connection.TrySend(OperationResult.Fail(type, "Error en el servidor: " + ex.Message));
                        context.Logger.Log(EventKind.Error, LogSeverity.Error,
                            "Error atendiendo " + type + " de " + session.Username + ": " + ex.Message);
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
                    "Centro de control desconectado (" + connection.RemoteText + ")");
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

            // Crear la sesión en el servidor
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
                // Administrador: empieza viendo la primera explotación que exista
                newSession.FarmName = "Todas las explotaciones";
                if (context.Store.GetFarms().Count > 0)
                {
                    newSession.ViewFarmId = context.Store.GetFarms()[0].Id;
                }
            }
            context.Sessions.Add(newSession);
            session = newSession;

            response.Success = true;
            response.Message = "Bienvenido, " + user.FullName;
            response.UserId = user.Id;
            response.Username = user.Username;
            response.FullName = user.FullName;
            response.Role = user.Role;
            response.FarmId = user.FarmId;
            response.FarmName = newSession.FarmName;
            response.SessionToken = newSession.Token;
            connection.Send(response);

            context.Logger.Log(EventKind.LoginSuccess, LogSeverity.Success,
                user.FullName + " (" + RolePermissions.DisplayName(user.Role) + ") inició sesión desde " + connection.RemoteText,
                user.FarmId == 0 ? (int?)null : user.FarmId, null, user.Username, null, null);
        }

        // ==================================================================
        // MENSAJES DESPUÉS DEL LOGIN
        // ==================================================================

        private void HandleMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.ChangePasswordRequest:
                    HandleChangePassword(MessageSerializer.FromJson<ChangePasswordRequest>(json));
                    break;

                // ----- Administración -----
                case MessageTypes.ListUsers:
                    if (Require(Permission.ManageUsers, type))
                    {
                        connection.Send(context.Users.ListUsers());
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
                            session.Username + " modificó la configuración del sistema");
                        connection.Send(OperationResult.Ok(type, "Configuración guardada"));
                    }
                    break;

                // ----- Logs -----
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
                        connection.Send(OperationResult.Fail(type, "Mensaje no reconocido: " + type));
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
                    session.Username + " intentó cambiar la contraseña: " + error, session.FarmId, null, session.Username, null, null);
                return;
            }

            connection.Send(OperationResult.Ok(request.Type, "Contraseña cambiada correctamente"));
            context.Logger.Log(EventKind.PasswordChanged, LogSeverity.Info,
                session.Username + " cambió su contraseña", session.FarmId, null, session.Username, null, null);
        }

        // ==================================================================
        // COMPROBACIONES DE SEGURIDAD
        // ==================================================================

        /// <summary>Comprueba que el rol del usuario tenga el permiso. Si no, responde con error y lo registra.</summary>
        private bool Require(Permission permission, string requestType)
        {
            if (RolePermissions.Has(session.Role, permission))
            {
                return true;
            }

            connection.Send(OperationResult.Fail(requestType,
                "Acceso denegado: su rol (" + RolePermissions.DisplayName(session.Role) + ") no permite esta acción"));
            context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                "DENEGADO: " + session.Username + " (" + session.Role + ") intentó " + requestType,
                session.FarmId, null, session.Username, null, null);
            return false;
        }

        /// <summary>
        /// Decide sobre qué explotación se trabaja (AISLAMIENTO DE DATOS).
        ///  - Usuario normal: SIEMPRE su propia explotación. Si pide otra, se deniega.
        ///  - Administrador: la que pida, o la que esté viendo si no indica ninguna.
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
                DenyOtherFarm(requestType, "la explotación " + requestedFarmId);
                return false;
            }
            return true;
        }

        private void DenyOtherFarm(string requestType, string what)
        {
            connection.Send(OperationResult.Fail(requestType, "Acceso denegado: " + what + " no pertenece a su explotación"));
            context.Logger.Log(EventKind.AccessDenied, LogSeverity.Security,
                "DENEGADO: " + session.Username + " (explotación " + session.FarmId + ") intentó " + requestType + " sobre " + what,
                session.FarmId, null, session.Username, null, null);
        }
    }
}
