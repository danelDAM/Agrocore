using System;
using System.Collections.Generic;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Security
{
    /// <summary>
    /// Gestión de usuarios y agricultores (solo la usa el administrador).
    /// Al crear un agricultor se crean a la vez: User + Farm (con campos, máquinas y cargadores).
    /// </summary>
    public class UserManagementService
    {
        private readonly DataStore store;
        private readonly SessionManager sessions;
        private readonly EventLogger logger;

        /// <summary>Se avisa cuando se crea una explotación (para ponerla en marcha en la simulación).</summary>
        public event Action<Farm> FarmCreated;

        /// <summary>Se avisa cuando se borra una explotación.</summary>
        public event Action<int> FarmDeleted;

        public UserManagementService(DataStore store, SessionManager sessions, EventLogger logger)
        {
            this.store = store;
            this.sessions = sessions;
            this.logger = logger;
        }

        public ListUsersResponse ListUsers()
        {
            ListUsersResponse response = new ListUsersResponse();
            List<Farm> farms = store.GetFarms();

            foreach (User user in store.GetUsers())
            {
                UserDto dto = new UserDto();
                dto.Id = user.Id;
                dto.Username = user.Username;
                dto.FullName = user.FullName;
                dto.Role = user.Role;
                dto.FarmId = user.FarmId;
                dto.IsActive = user.IsActive;
                dto.LastLogin = user.LastLogin;
                foreach (Farm farm in farms)
                {
                    if (farm.Id == user.FarmId)
                    {
                        dto.FarmName = farm.Name;
                    }
                }
                response.Users.Add(dto);
            }

            foreach (Farm farm in farms)
            {
                FarmDto dto = new FarmDto();
                dto.Id = farm.Id;
                dto.Name = farm.Name;
                dto.OwnerId = farm.OwnerId;
                dto.FieldCount = farm.FieldCount;
                dto.ChargerCount = farm.ChargerCount;
                dto.MachineCount = store.GetMachines(farm.Id).Count;
                User owner = store.GetUser(farm.OwnerId);
                if (owner != null)
                {
                    dto.OwnerName = owner.FullName;
                }
                response.Farms.Add(dto);
            }
            return response;
        }

        public CreateFarmerResponse CreateFarmer(CreateFarmerRequest request, string adminName)
        {
            CreateFarmerResponse response = new CreateFarmerResponse();

            string error = ValidateNewUser(request.Username, request.Password, request.FullName);
            if (error == null && string.IsNullOrWhiteSpace(request.FarmName))
            {
                error = "Indique el nombre de la explotación";
            }
            int machines = request.Seeders + request.Irrigators + request.Sprayers + request.Harvesters;
            if (error == null && (machines < 1 || machines > 16))
            {
                error = "La flota debe tener entre 1 y 16 máquinas";
            }
            if (error != null)
            {
                response.Success = false;
                response.Message = error;
                return response;
            }

            // 1. Usuario
            User user = store.AddUser(request.Username.Trim(), request.Password, request.FullName.Trim(), UserRole.Farmer, 0);

            // 2. Explotación con su flota. La clave de flota se genera aleatoria y solo se guarda su hash.
            string fleetKey = PasswordHasher.GenerateKey();
            Farm farm = store.CreateFarm(request.FarmName.Trim(), user.Id, request.FieldCount,
                Math.Max(0, request.Seeders), Math.Max(0, request.Irrigators), Math.Max(0, request.Sprayers),
                Math.Max(0, request.Harvesters), request.Chargers, fleetKey);

            // 3. Relación entre ambos (User.FarmId <-> Farm.OwnerId)
            store.SetUserFarm(user.Id, farm.Id);
            User saved = store.GetUser(user.Id);

            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " creó el agricultor " + saved.FullName + " (" + saved.Username + ") con la explotación " + farm.Name,
                farm.Id, null, adminName, null, null);

            if (FarmCreated != null)
            {
                FarmCreated(store.GetFarm(farm.Id));
            }

            response.Success = true;
            response.Message = "Agricultor y explotación creados";
            response.UserId = saved.Id;
            response.FarmId = farm.Id;
            response.FleetKey = fleetKey;
            return response;
        }

        public OperationResult CreateUser(CreateUserRequest request, string adminName)
        {
            string error = ValidateNewUser(request.Username, request.Password, request.FullName);
            if (error == null && request.Role != UserRole.Admin && store.GetFarm(request.FarmId) == null)
            {
                error = "La explotación indicada no existe";
            }
            if (error == null && request.Role == UserRole.Farmer)
            {
                error = "Para crear un agricultor use NUEVO AGRICULTOR (crea también su explotación)";
            }
            if (error != null)
            {
                return OperationResult.Fail(request.Type, error);
            }

            int farmId = request.Role == UserRole.Admin ? 0 : request.FarmId;
            User user = store.AddUser(request.Username.Trim(), request.Password, request.FullName.Trim(), request.Role, farmId);
            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " creó el usuario " + user.Username + " (" + request.Role + ")", farmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Usuario creado");
        }

        public OperationResult UpdateUser(UpdateUserRequest request, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Usuario no encontrado");
            }

            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                store.SetFullName(user.Id, request.FullName.Trim());
            }
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                string error = AuthService.ValidatePassword(request.NewPassword);
                if (error != null)
                {
                    return OperationResult.Fail(request.Type, error);
                }
                store.SetPassword(user.Id, request.NewPassword);
            }
            if (!string.IsNullOrWhiteSpace(request.FarmName) && user.Role == UserRole.Farmer)
            {
                store.RenameFarm(user.FarmId, request.FarmName.Trim());
            }

            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " modificó el usuario " + user.Username, user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Usuario modificado");
        }

        public OperationResult SetActive(SetUserActiveRequest request, int adminId, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Usuario no encontrado");
            }
            if (user.Id == adminId)
            {
                return OperationResult.Fail(request.Type, "No puede desactivar su propio usuario");
            }

            store.SetUserActive(user.Id, request.Active);
            if (!request.Active)
            {
                ForceLogout(user.Id, "Su usuario ha sido desactivado por el administrador");
            }

            string action = request.Active ? "activó" : "desactivó";
            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " " + action + " el usuario " + user.Username, user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, request.Active ? "Usuario activado" : "Usuario desactivado");
        }

        public OperationResult DeleteUser(DeleteUserRequest request, int adminId, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Usuario no encontrado");
            }
            if (user.Id == adminId)
            {
                return OperationResult.Fail(request.Type, "No puede eliminar su propio usuario");
            }

            ForceLogout(user.Id, "Su usuario ha sido eliminado");

            if (user.Role == UserRole.Farmer)
            {
                // Eliminar un agricultor elimina también su explotación y sus usuarios asociados.
                foreach (User other in store.GetUsers())
                {
                    if (other.FarmId == user.FarmId && other.Role != UserRole.Admin)
                    {
                        ForceLogout(other.Id, "La explotación ha sido eliminada");
                    }
                }
                int farmId = user.FarmId;
                store.DeleteFarm(farmId);
                if (FarmDeleted != null)
                {
                    FarmDeleted(farmId);
                }
            }
            store.DeleteUser(user.Id);

            logger.Log(EventKind.UserManagement, LogSeverity.Warning,
                adminName + " eliminó el usuario " + user.Username, user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Usuario eliminado");
        }

        public CreateFarmerResponse RegenerateFleetKey(RegenerateFleetKeyRequest request, string adminName)
        {
            CreateFarmerResponse response = new CreateFarmerResponse();
            Farm farm = store.GetFarm(request.FarmId);
            if (farm == null)
            {
                response.Success = false;
                response.Message = "La explotación no existe";
                return response;
            }
            string key = PasswordHasher.GenerateKey();
            store.SetFleetKey(farm.Id, key);
            logger.Log(EventKind.UserManagement, LogSeverity.Warning,
                adminName + " regeneró la clave de flota de " + farm.Name, farm.Id, null, adminName, null, null);

            response.Success = true;
            response.Message = "Nueva clave de flota generada";
            response.FarmId = farm.Id;
            response.FleetKey = key;
            return response;
        }

        private string ValidateNewUser(string username, string password, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            {
                return "El usuario debe tener al menos 3 caracteres";
            }
            foreach (char c in username.Trim())
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_')
                {
                    return "El usuario solo puede tener letras, números, '.' y '_'";
                }
            }
            if (store.FindUserByUsername(username.Trim()) != null)
            {
                return "Ya existe un usuario con ese nombre";
            }
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "Indique el nombre completo";
            }
            return AuthService.ValidatePassword(password);
        }

        /// <summary>Cierra las sesiones abiertas de un usuario (desactivado o eliminado).</summary>
        private void ForceLogout(int userId, string reason)
        {
            foreach (UserSession session in sessions.FindByUser(userId))
            {
                ForceLogoutNotice notice = new ForceLogoutNotice();
                notice.Reason = reason;
                session.Connection.TrySend(notice);
                sessions.Remove(session.Token);
                session.Connection.Close();
            }
        }
    }
}
