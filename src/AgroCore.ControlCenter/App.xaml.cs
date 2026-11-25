using System.Windows;
using AgroCore.ControlCenter.Services;
using AgroCore.ControlCenter.Views;
using AgroCore.Shared.Messages;

namespace AgroCore.ControlCenter
{
    /// <summary>
    /// Kontrol Zentroaren sarrera-puntua.
    /// Fluxua: saioa hasteko leihoa -> leiho nagusia -> (saioa itxi) -> saioa hasteko leihoa...
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShowLogin(null);
        }

        /// <summary>Saioa hasteko leihoa erakusten du (aukeran ohar batekin, adib. "saioa itxita").</summary>
        public void ShowLogin(string notice)
        {
            LoginWindow login = new LoginWindow(notice);
            login.Show();
        }

        /// <summary>Saioa ondo hasi denean deitzen da: leiho nagusia irekitzen du.</summary>
        public void ShowMain(ServerConnection connection, LoginResponse user)
        {
            MainWindow main = new MainWindow(connection, user);
            main.Show();
        }
    }
}
