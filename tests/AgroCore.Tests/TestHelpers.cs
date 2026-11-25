using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using AgroCore.Server;
using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Server.Security;

namespace AgroCore.Tests
{
    /// <summary>Crea un servidor completo en una carpeta temporal para las pruebas.</summary>
    public static class TestHelpers
    {
        public static ServerContext CreateContext()
        {
            string folder = Path.Combine(Path.GetTempPath(), "agrocore-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            ServerContext context = new ServerContext();
            context.Config = new ConfigStore(Path.Combine(folder, "server-config.json"));
            context.Logger = new EventLogger(Path.Combine(folder, "logs"));
            context.Logger.Start();
            context.Store = new DataStore(Path.Combine(folder, "data.json"));
            context.Store.Load();
            context.Sessions = new SessionManager();
            context.Auth = new AuthService(context.Store, context.Config, context.Logger);
            context.Users = new UserManagementService(context.Store, context.Sessions, context.Logger);
            return context;
        }

        public static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
