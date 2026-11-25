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
    /// Erabiltzaileen eta nekazarien kudeaketa (administratzaileak bakarrik erabiltzen du).
    /// Nekazari bat sortzean, aldi berean sortzen dira: User + Farm (soro, makina eta kargagailuekin).
    /// </summary>
    public class UserManagementService
    {
        private readonly DataStore store;
        private readonly SessionManager sessions;
        private readonly EventLogger logger;

        /// <summary>Ustiategi bat sortzen denean abisatzen da (simulazioan martxan jartzeko).</summary>
        public event Action<Farm> FarmCreated;

        /// <summary>Ustiategi bat ezabatzen denean abisatzen da.</summary>
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
                error = "Adierazi ustiategiaren izena";
            }
            int machines = request.Seeders + request.Irrigators + request.Sprayers + request.Harvesters;
            if (error == null && (machines < 1 || machines > 16))
            {
                error = "Flotak 1 eta 16 makina artean izan behar ditu";
            }
            if (error != null)
            {
                response.Success = false;
                response.Message = error;
                return response;
            }

            // 1. Erabiltzailea
            User user = store.AddUser(request.Username.Trim(), request.Password, request.FullName.Trim(), UserRole.Farmer, 0);

            // 2. Ustiategia bere flotarekin. Flota-gakoa ausaz sortzen da eta haren hasha bakarrik gordetzen da.
            string fleetKey = PasswordHasher.GenerateKey();
            Farm farm = store.CreateFarm(request.FarmName.Trim(), user.Id, request.FieldCount,
                Math.Max(0, request.Seeders), Math.Max(0, request.Irrigators), Math.Max(0, request.Sprayers),
                Math.Max(0, request.Harvesters), request.Chargers, fleetKey);

            // 3. Bien arteko erlazioa (User.FarmId <-> Farm.OwnerId)
            store.SetUserFarm(user.Id, farm.Id);
            User saved = store.GetUser(user.Id);

            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " erabiltzaileak " + saved.FullName + " (" + saved.Username + ") nekazaria sortu du, " + farm.Name + " ustiategiarekin",
                farm.Id, null, adminName, null, null);

            if (FarmCreated != null)
            {
                FarmCreated(store.GetFarm(farm.Id));
            }

            response.Success = true;
            response.Message = "Nekazaria eta ustiategia sortuta";
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
                error = "Adierazitako ustiategia ez da existitzen";
            }
            if (error == null && request.Role == UserRole.Farmer)
            {
                error = "Nekazari bat sortzeko, erabili NEKAZARI BERRIA (bere ustiategia ere sortzen du)";
            }
            if (error != null)
            {
                return OperationResult.Fail(request.Type, error);
            }

            int farmId = request.Role == UserRole.Admin ? 0 : request.FarmId;
            User user = store.AddUser(request.Username.Trim(), request.Password, request.FullName.Trim(), request.Role, farmId);
            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " erabiltzaileak " + user.Username + " erabiltzailea sortu du (" + request.Role + ")", farmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Erabiltzailea sortuta");
        }

        public OperationResult UpdateUser(UpdateUserRequest request, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Ez da erabiltzailea aurkitu");
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
                adminName + " erabiltzaileak " + user.Username + " erabiltzailea aldatu du", user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Erabiltzailea aldatuta");
        }

        public OperationResult SetActive(SetUserActiveRequest request, int adminId, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Ez da erabiltzailea aurkitu");
            }
            if (user.Id == adminId)
            {
                return OperationResult.Fail(request.Type, "Ezin duzu zure erabiltzailea desaktibatu");
            }

            store.SetUserActive(user.Id, request.Active);
            if (!request.Active)
            {
                ForceLogout(user.Id, "Administratzaileak zure erabiltzailea desaktibatu du");
            }

            string action = request.Active ? "aktibatu" : "desaktibatu";
            logger.Log(EventKind.UserManagement, LogSeverity.Info,
                adminName + " erabiltzaileak " + user.Username + " erabiltzailea " + action + " du", user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, request.Active ? "Erabiltzailea aktibatuta" : "Erabiltzailea desaktibatuta");
        }

        public OperationResult DeleteUser(DeleteUserRequest request, int adminId, string adminName)
        {
            User user = store.GetUser(request.UserId);
            if (user == null)
            {
                return OperationResult.Fail(request.Type, "Ez da erabiltzailea aurkitu");
            }
            if (user.Id == adminId)
            {
                return OperationResult.Fail(request.Type, "Ezin duzu zure erabiltzailea ezabatu");
            }

            ForceLogout(user.Id, "Zure erabiltzailea ezabatu egin da");

            if (user.Role == UserRole.Farmer)
            {
                // Nekazari bat ezabatzean, bere ustiategia eta lotutako erabiltzaileak ere ezabatzen dira.
                foreach (User other in store.GetUsers())
                {
                    if (other.FarmId == user.FarmId && other.Role != UserRole.Admin)
                    {
                        ForceLogout(other.Id, "Ustiategia ezabatu egin da");
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
                adminName + " erabiltzaileak " + user.Username + " erabiltzailea ezabatu du", user.FarmId, null, adminName, null, null);
            return OperationResult.Ok(request.Type, "Erabiltzailea ezabatuta");
        }

        public CreateFarmerResponse RegenerateFleetKey(RegenerateFleetKeyRequest request, string adminName)
        {
            CreateFarmerResponse response = new CreateFarmerResponse();
            Farm farm = store.GetFarm(request.FarmId);
            if (farm == null)
            {
                response.Success = false;
                response.Message = "Ustiategia ez da existitzen";
                return response;
            }
            string key = PasswordHasher.GenerateKey();
            store.SetFleetKey(farm.Id, key);
            logger.Log(EventKind.UserManagement, LogSeverity.Warning,
                adminName + " erabiltzaileak " + farm.Name + " ustiategiaren flota-gakoa berriro sortu du", farm.Id, null, adminName, null, null);

            response.Success = true;
            response.Message = "Flota-gako berria sortuta";
            response.FarmId = farm.Id;
            response.FleetKey = key;
            return response;
        }

        private string ValidateNewUser(string username, string password, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            {
                return "Erabiltzaileak gutxienez 3 karaktere izan behar ditu";
            }
            foreach (char c in username.Trim())
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_')
                {
                    return "Erabiltzaileak letrak, zenbakiak, '.' eta '_' bakarrik izan ditzake";
                }
            }
            if (store.FindUserByUsername(username.Trim()) != null)
            {
                return "Badago izen hori duen erabiltzaile bat";
            }
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "Adierazi izen osoa";
            }
            return AuthService.ValidatePassword(password);
        }

        /// <summary>Erabiltzaile baten saio irekiak ixten ditu (desaktibatuta edo ezabatuta).</summary>
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
