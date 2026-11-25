using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgroCore.ControlCenter.Models;
using AgroCore.ControlCenter.Services;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Networking;
using AgroCore.Shared.Security;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>Datos del usuario y cambio de contraseña (el servidor valida la contraseña actual).</summary>
    public partial class ProfilePage : UserControl
    {
        private readonly MainWindow main;

        public ProfilePage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;

            NameText.Text = main.User.FullName;
            UserText.Text = "Usuario: " + main.User.Username;
            RoleText.Text = "Rol: " + Texts.Role(main.User.Role);
            FarmText.Text = "Explotación: " + (main.User.Role == UserRole.Admin ? "todas" : main.User.FarmName);
            LocalSessionData session = LocalSession.Load();
            if (session.LoginTime.HasValue)
            {
                LoginText.Text = "Sesión iniciada: " + session.LoginTime.Value.ToString("dd/MM/yyyy HH:mm");
            }

            // Lista de lo que puede hacer este rol
            string text = "Permisos: ";
            foreach (Permission permission in Enum.GetValues(typeof(Permission)))
            {
                if (RolePermissions.Has(main.User.Role, permission))
                {
                    text = text + permission + ", ";
                }
            }
            PermissionsText.Text = text.TrimEnd(' ', ',');
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentBox.Password == "" || NewBox.Password == "")
            {
                ShowResult("Rellene todos los campos.", false);
                return;
            }
            if (NewBox.Password != RepeatBox.Password)
            {
                ShowResult("Las dos contraseñas nuevas no coinciden.", false);
                return;
            }

            ChangePasswordRequest request = new ChangePasswordRequest();
            request.CurrentPassword = CurrentBox.Password;
            request.NewPassword = NewBox.Password;
            main.Send(request);
            ChangeButton.IsEnabled = false;
        }

        private void OnMessage(string type, string json)
        {
            if (type != MessageTypes.Result)
            {
                return;
            }
            OperationResult result = MessageSerializer.FromJson<OperationResult>(json);
            if (result.RequestType != MessageTypes.ChangePasswordRequest)
            {
                return;
            }

            ChangeButton.IsEnabled = true;
            ShowResult(result.Message, result.Success);
            if (result.Success)
            {
                CurrentBox.Clear();
                NewBox.Clear();
                RepeatBox.Clear();
            }
        }

        private void ShowResult(string text, bool ok)
        {
            ResultText.Text = text;
            ResultText.Foreground = ok ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("DangerBrush");
        }
    }
}
