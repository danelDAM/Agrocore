using System.Collections.Generic;
using System.Threading;
using AgroCore.Server;
using AgroCore.Server.Networking;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;
using Xunit;

namespace AgroCore.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void Json_TieneElFormatoDelProtocolo()
        {
            LoginRequest request = new LoginRequest();
            request.Username = "juan";
            request.Password = "x";

            string json = MessageSerializer.ToJson(request);

            Assert.Contains("\"type\":\"LOGIN_REQUEST\"", json);
            Assert.Contains("\"username\":\"juan\"", json);
            Assert.Equal(MessageTypes.LoginRequest, MessageSerializer.ReadType(json));
        }

        [Fact]
        public void Json_EnumsEnMayusculas()
        {
            LoginResponse response = new LoginResponse();
            response.Role = UserRole.Farmer;

            string json = MessageSerializer.ToJson(response);

            Assert.Contains("\"role\":\"FARMER\"", json);
            Assert.Equal(UserRole.Farmer, MessageSerializer.FromJson<LoginResponse>(json).Role);
        }

        [Fact]
        public void Plano_CaminoDesdeLaBaseHastaUnCampo()
        {
            FarmLayout layout = new FarmLayout(6, 3, 11);
            int entranceCol = layout.FieldEntranceCol(5);
            int entranceRow = layout.FieldEntranceRow(5);

            List<int[]> path = layout.FindPath(FarmLayout.BaseLaneCol, 0, entranceCol, entranceRow);

            Assert.NotEmpty(path);
            // Gelaxka guztiak zirkulagarriak eta jarraian daude
            for (int i = 0; i < path.Count; i++)
            {
                Assert.True(layout.IsDrivable(path[i][0], path[i][1]));
                if (i > 0)
                {
                    Assert.True(layout.CanConnect(path[i - 1][0], path[i - 1][1], path[i][0], path[i][1]));
                }
            }
            // Basetik atetik irteten da
            bool passedGate = false;
            foreach (int[] cell in path)
            {
                if (cell[0] == 3 && cell[1] == layout.GateRow) passedGate = true;
            }
            Assert.True(passedGate);
        }

        /// <summary>Integrazio-proba: saio-hasiera ERREALA TCP bidez zerbitzariaren aurka.</summary>
        [Fact]
        public void Tcp_LoginContraElServidor()
        {
            ServerContext context = TestHelpers.CreateContext();
            int port = TestHelpers.GetFreePort();
            TcpServer server = new TcpServer(context, port);
            server.Start();

            try
            {
                // 1. Pasahitz okerra
                Connection connection = Connection.Connect("127.0.0.1", port);
                LoginRequest bad = new LoginRequest();
                bad.Username = "juan";
                bad.Password = "mala";
                connection.Send(bad);
                LoginResponse badResponse = MessageSerializer.FromJson<LoginResponse>(connection.ReadLine());
                Assert.False(badResponse.Success);
                Assert.Equal("Erabiltzailea edo pasahitza okerrak dira", badResponse.Message);

                // 2. Konexio berean, pasahitz zuzena
                LoginRequest good = new LoginRequest();
                good.Username = "juan";
                good.Password = "juan123";
                connection.Send(good);
                LoginResponse goodResponse = MessageSerializer.FromJson<LoginResponse>(connection.ReadLine());
                Assert.True(goodResponse.Success);
                Assert.Equal(UserRole.Farmer, goodResponse.Role);
                Assert.Equal("Esperantza baserria", goodResponse.FarmName);
                Assert.False(string.IsNullOrEmpty(goodResponse.SessionToken));
                Assert.Equal(1, context.Sessions.Count);

                // 3. Nekazari batek EZIN ditu erabiltzaileak kudeatu
                connection.Send(new ListUsersRequest());
                OperationResult denied = MessageSerializer.FromJson<OperationResult>(connection.ReadLine());
                Assert.False(denied.Success);
                Assert.Contains("Sarbidea ukatuta", denied.Message);

                // 4. Saioa itxi: zerbitzariak saioa ezabatzen du
                connection.Send(new LogoutRequest());
                OperationResult logout = MessageSerializer.FromJson<OperationResult>(connection.ReadLine());
                Assert.True(logout.Success);
                Thread.Sleep(200);
                Assert.Equal(0, context.Sessions.Count);
                connection.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public void Tcp_AdminPuedeListarUsuarios()
        {
            ServerContext context = TestHelpers.CreateContext();
            int port = TestHelpers.GetFreePort();
            TcpServer server = new TcpServer(context, port);
            server.Start();
            try
            {
                Connection connection = Connection.Connect("127.0.0.1", port);
                LoginRequest login = new LoginRequest();
                login.Username = "admin";
                login.Password = "admin123";
                connection.Send(login);
                Assert.True(MessageSerializer.FromJson<LoginResponse>(connection.ReadLine()).Success);

                connection.Send(new ListUsersRequest());
                string line = connection.ReadLine();
                Assert.Equal(MessageTypes.UsersResponse, MessageSerializer.ReadType(line));
                ListUsersResponse users = MessageSerializer.FromJson<ListUsersResponse>(line);
                Assert.Equal(6, users.Users.Count);
                Assert.Equal(3, users.Farms.Count);
                connection.Close();
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
