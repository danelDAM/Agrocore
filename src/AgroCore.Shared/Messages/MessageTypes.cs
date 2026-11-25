namespace AgroCore.Shared.Messages
{
    /// <summary>AGROCORE protokoloaren mezu mota guztiak.</summary>
    public static class MessageTypes
    {
        // ----- Saio-hasiera eta saioa (WPF <-> zerbitzaria, TCP) -----
        public const string LoginRequest = "LOGIN_REQUEST";
        public const string LoginResponse = "LOGIN_RESPONSE";
        public const string LogoutRequest = "LOGOUT_REQUEST";
        public const string ChangePasswordRequest = "CHANGE_PASSWORD_REQUEST";
        public const string Result = "RESULT";
        public const string ForceLogout = "FORCE_LOGOUT";

        // ----- Makinak (makina <-> zerbitzaria, TCP) -----
        public const string MachineAuth = "MACHINE_AUTH";
        public const string MachineAuthResponse = "MACHINE_AUTH_RESPONSE";
        public const string FleetQuery = "FLEET_QUERY";
        public const string FleetResponse = "FLEET_RESPONSE";
        public const string TaskRequest = "TASK_REQUEST";
        public const string TaskAssignment = "TASK_ASSIGNMENT";
        public const string TaskStarted = "TASK_STARTED";
        public const string TaskCompleted = "TASK_COMPLETED";
        public const string TaskAborted = "TASK_ABORTED";
        public const string TaskCancelled = "TASK_CANCELLED";
        public const string ZoneRequest = "ZONE_REQUEST";
        public const string ZoneResponse = "ZONE_RESPONSE";
        public const string ZoneRelease = "ZONE_RELEASE";
        public const string ChargerRequest = "CHARGER_REQUEST";
        public const string ChargerResponse = "CHARGER_RESPONSE";
        public const string ChargerRelease = "CHARGER_RELEASE";
        public const string MachineEvent = "MACHINE_EVENT";

        // ----- Aginduak (WPF -> zerbitzaria -> makina, TCP) -----
        public const string Start = "START";
        public const string Stop = "STOP";
        public const string Resume = "RESUME";
        public const string ReturnToBase = "RETURN_TO_BASE";
        public const string EmergencyStop = "EMERGENCY_STOP";
        public const string StopAll = "STOP_ALL";
        public const string ResumeAll = "RESUME_ALL";
        public const string ReturnAllToBase = "RETURN_ALL_TO_BASE";
        public const string SetSimulationSpeed = "SET_SIMULATION_SPEED";

        // ----- Ustiategia (WPF <-> zerbitzaria, TCP) -----
        public const string SelectFarm = "SELECT_FARM";
        public const string UdpSubscribe = "UDP_SUBSCRIBE";
        public const string FarmState = "FARM_STATE";
        public const string CreateCropPlan = "CREATE_CROP_PLAN";
        public const string CancelTask = "CANCEL_TASK";
        public const string StatisticsRequest = "STATISTICS_REQUEST";
        public const string StatisticsResponse = "STATISTICS_RESPONSE";
        public const string GlobalStatisticsRequest = "GLOBAL_STATISTICS_REQUEST";
        public const string GlobalStatisticsResponse = "GLOBAL_STATISTICS_RESPONSE";
        public const string ReportRequest = "REPORT_REQUEST";
        public const string ReportResponse = "REPORT_RESPONSE";
        public const string LogsRequest = "LOGS_REQUEST";
        public const string LogsResponse = "LOGS_RESPONSE";

        // ----- Administrazioa (WPF <-> zerbitzaria, TCP) -----
        public const string ListUsers = "LIST_USERS";
        public const string UsersResponse = "USERS_RESPONSE";
        public const string CreateFarmer = "CREATE_FARMER";
        public const string CreateFarmerResponse = "CREATE_FARMER_RESPONSE";
        public const string CreateUser = "CREATE_USER";
        public const string UpdateUser = "UPDATE_USER";
        public const string SetUserActive = "SET_USER_ACTIVE";
        public const string DeleteUser = "DELETE_USER";
        public const string RegenerateFleetKey = "REGENERATE_FLEET_KEY";
        public const string GetConfig = "GET_CONFIG";
        public const string ConfigResponse = "CONFIG_RESPONSE";
        public const string UpdateConfig = "UPDATE_CONFIG";

        // ----- Telemetria (UDP) -----
        public const string PositionUpdate = "POSITION_UPDATE";
        public const string TelemetryBatch = "TELEMETRY_BATCH";
    }
}
