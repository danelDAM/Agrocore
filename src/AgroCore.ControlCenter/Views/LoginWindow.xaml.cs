using System;
using System.Threading;
using System.Windows;
using AgroCore.ControlCenter.Services;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Ventana de login. El usuario y la contraseña se comprueban SIEMPRE en el servidor:
    /// el Centro de Control no sabe nada de contraseñas.
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
                    Application.Current.Shutdown(); // cerrar el login sin entrar = salir del programa
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
                ShowError("Escriba el usuario y la contraseña.");
                return;
            }

            // "servidor" o "servidor:puerto"
            string host = hostText;
            int port = NetworkDefaults.TcpPort;
            int colon = hostText.LastIndexOf(':');
            if (colon > 0)
            {
                host = hostText.Substring(0, colon);
                if (!int.TryParse(hostText.Substring(colon + 1), out port))
                {
                    ShowError("El puerto del servidor no es válido.");
                    return;
                }
            }
            if (host == "")
            {
                host = NetworkDefaults.Host;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "CONECTANDO...";
            ErrorPanel.Visibility = Visibility.Collapsed;
            NoticePanel.Visibility = Visibility.Collapsed;

            // La conexión se hace en otro hilo para que la ventana no se congele
            Thread thread = new Thread(() => DoLogin(host, port, username, password));
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>Se ejecuta en un hilo secundario.</summary>
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
                    error = "El servidor ha cerrado la conexión.";
                }
                else if (!response.Success)
                {
                    error = response.Message;
                }
            }
            catch (Exception)
            {
                error = "No se puede conectar con el servidor " + host + ":" + port + ". Compruebe que está arrancado.";
            }

            if (error != null)
            {
                connection.Close();
            }

            // Volver al hilo de la interfaz para tocar los controles
            Dispatcher.BeginInvoke(new Action(() => LoginFinished(connection, response, error, host, port, username)));
        }

        private void LoginFinished(ServerConnection connection, LoginResponse response, string error,
                                   string host, int port, string username)
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "ENTRAR";

            if (error != null)
            {
                ShowError(error);
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            // Guardar la sesión local
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
                "• Compruebe que el servidor AGROCORE está arrancado y que la dirección es correcta.\n" +
                "• Tras 5 intentos fallidos la cuenta se bloquea 30 segundos.\n" +
                "• Si ha olvidado la contraseña, pida al administrador que le asigne una nueva.\n\n" +
                "Usuarios de demostración: admin/admin123, juan/juan123, maria/maria123, pedro/pedro123",
                "Ayuda de acceso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
