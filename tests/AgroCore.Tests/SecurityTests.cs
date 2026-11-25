using AgroCore.Server;
using AgroCore.Server.Security;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Security;
using Xunit;

namespace AgroCore.Tests
{
    public class SecurityTests
    {
        [Fact]
        public void Hash_NoGuardaTextoPlano_YVerificaCorrectamente()
        {
            string hash = PasswordHasher.Hash("juan123");

            Assert.DoesNotContain("juan123", hash);
            Assert.StartsWith("PBKDF2$", hash);
            Assert.True(PasswordHasher.Verify("juan123", hash));
            Assert.False(PasswordHasher.Verify("juan124", hash));
        }

        [Fact]
        public void Hash_MismaContrasena_DaHashesDistintos()
        {
            // ausazko gatzagatik
            Assert.NotEqual(PasswordHasher.Hash("abc123"), PasswordHasher.Hash("abc123"));
        }

        [Fact]
        public void Permisos_SegunRol()
        {
            Assert.True(RolePermissions.Has(UserRole.Admin, Permission.ManageUsers));
            Assert.False(RolePermissions.Has(UserRole.Farmer, Permission.ManageUsers));
            Assert.True(RolePermissions.Has(UserRole.Farmer, Permission.GenerateReports));
            Assert.True(RolePermissions.Has(UserRole.Operator, Permission.ControlMachines));
            Assert.False(RolePermissions.Has(UserRole.Operator, Permission.ManageCropPlans));
            Assert.False(RolePermissions.Has(UserRole.Viewer, Permission.ControlMachines));
            Assert.True(RolePermissions.Has(UserRole.Viewer, Permission.ViewStatistics));
        }

        [Fact]
        public void Login_Correcto_DevuelveUsuarioYExplotacion()
        {
            ServerContext context = TestHelpers.CreateContext();

            LoginResult result = context.Auth.Login("juan", "juan123", "test");

            Assert.True(result.Success);
            Assert.Equal("Juan García", result.User.FullName);
            Assert.Equal(UserRole.Farmer, result.User.Role);
            Assert.Equal("Esperantza baserria", result.Farm.Name);
        }

        [Fact]
        public void Login_ContrasenaIncorrecta_MensajeGenerico()
        {
            ServerContext context = TestHelpers.CreateContext();

            LoginResult wrongPassword = context.Auth.Login("juan", "mala", "test");
            LoginResult unknownUser = context.Auth.Login("noexiste", "mala", "test");

            Assert.False(wrongPassword.Success);
            Assert.Equal("Erabiltzailea edo pasahitza okerrak dira", wrongPassword.Message);
            Assert.Equal(wrongPassword.Message, unknownUser.Message);
        }

        [Fact]
        public void Login_UsuarioDesactivado_Rechazado()
        {
            ServerContext context = TestHelpers.CreateContext();
            int mariaId = context.Store.FindUserByUsername("maria").Id;
            context.Store.SetUserActive(mariaId, false);

            LoginResult result = context.Auth.Login("maria", "maria123", "test");

            Assert.False(result.Success);
            Assert.Contains("desaktibatu", result.Message);
        }

        [Fact]
        public void Login_BloqueoTrasVariosFallos()
        {
            ServerContext context = TestHelpers.CreateContext();
            for (int i = 0; i < 5; i++)
            {
                context.Auth.Login("pedro", "mala", "test");
            }

            // Orain pasahitza ondo jarri arren, denbora batez blokeatuta dago
            LoginResult result = context.Auth.Login("pedro", "pedro123", "test");

            Assert.False(result.Success);
            Assert.Contains("Saiakera gehiegi", result.Message);
        }

        [Fact]
        public void CambioContrasena_ExigeLaActual()
        {
            ServerContext context = TestHelpers.CreateContext();
            int juanId = context.Store.FindUserByUsername("juan").Id;

            Assert.NotNull(context.Auth.ChangePassword(juanId, "incorrecta", "nueva123"));
            Assert.Null(context.Auth.ChangePassword(juanId, "juan123", "nueva123"));
            Assert.True(context.Auth.Login("juan", "nueva123", "test").Success);
        }

        [Fact]
        public void CrearAgricultor_CreaUsuarioYExplotacionRelacionados()
        {
            ServerContext context = TestHelpers.CreateContext();
            AgroCore.Shared.Messages.CreateFarmerRequest request = new AgroCore.Shared.Messages.CreateFarmerRequest();
            request.FullName = "Lucía Gómez";
            request.Username = "lucia";
            request.Password = "lucia123";
            request.FarmName = "Sahats baserria";

            AgroCore.Shared.Messages.CreateFarmerResponse response = context.Users.CreateFarmer(request, "admin");

            Assert.True(response.Success, response.Message);
            Assert.False(string.IsNullOrEmpty(response.FleetKey));
            var user = context.Store.FindUserByUsername("lucia");
            var farm = context.Store.GetFarm(response.FarmId);
            Assert.Equal(farm.Id, user.FarmId);
            Assert.Equal(user.Id, farm.OwnerId);
            Assert.Equal(11, context.Store.GetMachines(farm.Id).Count);
            Assert.Equal(6, context.Store.GetFields(farm.Id).Count);
        }
    }
}
