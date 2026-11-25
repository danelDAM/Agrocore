using System.Collections.Generic;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    public class ListUsersRequest : NetMessage
    {
        public ListUsersRequest()
        {
            Type = MessageTypes.ListUsers;
        }
    }

    public class ListUsersResponse : ResponseMessage
    {
        public List<UserDto> Users { get; set; } = new List<UserDto>();
        public List<FarmDto> Farms { get; set; } = new List<FarmDto>();

        public ListUsersResponse()
        {
            Type = MessageTypes.UsersResponse;
        }
    }

    /// <summary>Crea el usuario agricultor + su explotación (campos, máquinas y cargadores).</summary>
    public class CreateFarmerRequest : NetMessage
    {
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FarmName { get; set; } = "";
        public int FieldCount { get; set; } = 6;
        public int Seeders { get; set; } = 2;
        public int Irrigators { get; set; } = 2;
        public int Sprayers { get; set; } = 3;
        public int Harvesters { get; set; } = 4;
        public int Chargers { get; set; } = 3;

        public CreateFarmerRequest()
        {
            Type = MessageTypes.CreateFarmer;
        }
    }

    public class CreateFarmerResponse : ResponseMessage
    {
        public int UserId { get; set; }
        public int FarmId { get; set; }

        /// <summary>Clave de flota en claro. Solo se muestra esta vez; el servidor guarda el hash.</summary>
        public string FleetKey { get; set; }

        public CreateFarmerResponse()
        {
            Type = MessageTypes.CreateFarmerResponse;
        }
    }

    /// <summary>Crea un operador u observador asignado a una explotación.</summary>
    public class CreateUserRequest : NetMessage
    {
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public UserRole Role { get; set; } = UserRole.Operator;
        public int FarmId { get; set; }

        public CreateUserRequest()
        {
            Type = MessageTypes.CreateUser;
        }
    }

    public class UpdateUserRequest : NetMessage
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string FarmName { get; set; }

        /// <summary>Si no está vacía, se cambia la contraseña.</summary>
        public string NewPassword { get; set; }

        public UpdateUserRequest()
        {
            Type = MessageTypes.UpdateUser;
        }
    }

    public class SetUserActiveRequest : NetMessage
    {
        public int UserId { get; set; }
        public bool Active { get; set; }

        public SetUserActiveRequest()
        {
            Type = MessageTypes.SetUserActive;
        }
    }

    public class DeleteUserRequest : NetMessage
    {
        public int UserId { get; set; }

        public DeleteUserRequest()
        {
            Type = MessageTypes.DeleteUser;
        }
    }

    public class RegenerateFleetKeyRequest : NetMessage
    {
        public int FarmId { get; set; }

        public RegenerateFleetKeyRequest()
        {
            Type = MessageTypes.RegenerateFleetKey;
        }
    }

    public class GetConfigRequest : NetMessage
    {
        public GetConfigRequest()
        {
            Type = MessageTypes.GetConfig;
        }
    }

    public class ConfigResponse : ResponseMessage
    {
        public SystemConfig Config { get; set; }

        public ConfigResponse()
        {
            Type = MessageTypes.ConfigResponse;
        }
    }

    public class UpdateConfigRequest : NetMessage
    {
        public SystemConfig Config { get; set; } = new SystemConfig();

        public UpdateConfigRequest()
        {
            Type = MessageTypes.UpdateConfig;
        }
    }
}
