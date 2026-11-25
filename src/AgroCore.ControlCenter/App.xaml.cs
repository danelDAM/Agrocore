using System.Windows;
using AgroCore.ControlCenter.Services;
using AgroCore.ControlCenter.Views;
using AgroCore.Shared.Messages;

namespace AgroCore.ControlCenter
{
    /// <summary>
    /// Punto de entrada del Centro de Control.
    /// Flujo: ventana de login -> ventana principal -> (cerrar sesión) -> ventana de login...
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShowLogin(null);
        }

        /// <summary>Muestra la ventana de login (opcionalmente con un aviso, p. ej. "sesión cerrada").</summary>
        public void ShowLogin(string notice)
        {
            LoginWindow login = new LoginWindow(notice);
            login.Show();
        }

        /// <summary>Se llama cuando el login ha ido bien: abre la ventana principal.</summary>
        public void ShowMain(ServerConnection connection, LoginResponse user)
        {
            MainWindow main = new MainWindow(connection, user);
            main.Show();
        }
    }
}
