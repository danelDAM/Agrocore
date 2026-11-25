using System;
using System.Threading;
using System.Windows;
using AgroCore.ControlCenter.Services;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Saioa hasteko leihoa. Erabiltzailea eta pasahitza BETI zerbitzarian egiaztatzen dira:
    /// Kontrol Zentroak ez daki ezer pasahitzei buruz.
    /// </summary>
    public partial class LoginWindow : Window
    {
        private bool loggedIn = false;

        public LoginWindow(string notice)
        {
            InitializeComponent();

            LocalSessionData saved = LocalSession.Load();
            HostBox.Text = saved.Host;
            UsernameBox.Text = saved.LastUsername;

            if (!string.IsNullOrEmpty(notice))
            {
                NoticeText.Text = notice;
                NoticePanel.Visibility = Visibility.Visible;
            }

            Loaded += (s, e) =>
            {
                if (UsernameBox.Text == "")
                {
                    UsernameBox.Focus();
                }
                else
                {
                    PasswordBox.Focus();
                }
            };

            Closed += (s, e) =>
            {
                if (!loggedIn)
                {
                    Application.Current.Shutdown(); // saioa hasi gabe leihoa ixtea = programatik irtetea
                }
            };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;
            string hostText = HostBox.Text.Trim();

            if (username == "" || password == "")
            {
                ShowError("Idatzi erabiltzailea eta pasahitza.");
                return;
            }

            // "zerbitzaria" edo "zerbitzaria:ataka"
            string host = hostText;
            int port = NetworkDefaults.TcpPort;
            int colon = hostText.LastIndexOf(':');
            if (colon > 0)
            {
                host = hostText.Substring(0, colon);
                if (!int.TryParse(hostText.Substring(colon + 1), out port))
                {
                    ShowError("Zerbitzariaren ataka ez da baliozkoa.");
                    return;
                }
            }
            if (host == "")
            {
                host = NetworkDefaults.Host;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "KONEKTATZEN...";
            ErrorPanel.Visibility = Visibility.Collapsed;
            NoticePanel.Visibility = Visibility.Collapsed;

            // Konexioa beste hari batean egiten da, leihoa izoztu ez dadin
            Thread thread = new Thread(() => DoLogin(host, port, username, password));
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>Bigarren mailako hari batean exekutatzen da.</summary>
        private void DoLogin(string host, int port, string username, string password)
        {
            ServerConnection connection = new ServerConnection(host, port);
            LoginResponse response = null;
            string error = null;

            try
            {
                connection.Connect();
                response = connection.Login(username, password);
                if (response == null)
                {
                    error = "Zerbitzariak konexioa itxi du.";
                }
                else if (!response.Success)
                {
                    error = response.Message;
                }
            }
            catch (Exception)
            {
                error = "Ezin da " + host + ":" + port + " zerbitzariarekin konektatu. Egiaztatu martxan dagoela.";
            }

            if (error != null)
            {
                connection.Close();
            }

            // Interfazearen harira itzuli kontrolak ukitzeko
            Dispatcher.BeginInvoke(new Action(() => LoginFinished(connection, response, error, host, port, username)));
        }

        private void LoginFinished(ServerConnection connection, LoginResponse response, string error,
                                   string host, int port, string username)
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "SAIOA HASI";

            if (error != null)
            {
                ShowError(error);
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            // Saio lokala gorde
            LocalSessionData data = LocalSession.Load();
            data.Host = port == NetworkDefaults.TcpPort ? host : host + ":" + port;
            data.LastUsername = username;
            data.Username = response.Username;
            data.FullName = response.FullName;
            data.FarmName = response.FarmName;
            data.LoginTime = DateTime.Now;
            LocalSession.Save(data);

            loggedIn = true;
            ((App)Application.Current).ShowMain(connection, response);
            Close();
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "• Egiaztatu AGROCORE zerbitzaria martxan dagoela eta helbidea zuzena dela.\n" +
                "• Huts egindako 5 saiakeraren ondoren, kontua 30 segundoz blokeatzen da.\n" +
                "• Pasahitza ahaztu baduzu, eskatu administratzaileari berri bat esleitzeko.\n\n" +
                "Demo-erabiltzaileak: admin/admin123, juan/juan123, maria/maria123, pedro/pedro123",
                "Sartzeko laguntza", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
