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
    /// <summary>Erabiltzailearen datuak eta pasahitz-aldaketa (zerbitzariak balidatzen du uneko pasahitza).</summary>
    public partial class ProfilePage : UserControl
    {
        private readonly MainWindow main;

        public ProfilePage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;

            NameText.Text = main.User.FullName;
            UserText.Text = "Erabiltzailea: " + main.User.Username;
            RoleText.Text = "Rola: " + Texts.Role(main.User.Role);
            FarmText.Text = "Ustiategia: " + (main.User.Role == UserRole.Admin ? "guztiak" : main.User.FarmName);
            LocalSessionData session = LocalSession.Load();
            if (session.LoginTime.HasValue)
            {
                LoginText.Text = "Saioa hasita: " + session.LoginTime.Value.ToString("dd/MM/yyyy HH:mm");
            }

            // Rol honek egin dezakeenaren zerrenda
            string text = "Baimenak: ";
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
                ShowResult("Bete eremu guztiak.", false);
                return;
            }
            if (NewBox.Password != RepeatBox.Password)
            {
                ShowResult("Bi pasahitz berriak ez datoz bat.", false);
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
