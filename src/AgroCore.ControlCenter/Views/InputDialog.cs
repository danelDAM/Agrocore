using System.Windows;
using System.Windows.Controls;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>
    /// Ventanita para pedir un texto (o una contraseña). Se crea por código para que sea sencilla:
    ///   string value = InputDialog.Ask(owner, "Título", "Pregunta", "valor inicial", false);
    /// Devuelve null si se cancela.
    /// </summary>
    public class InputDialog : Window
    {
        private readonly TextBox textBox = new TextBox();
        private readonly PasswordBox passwordBox = new PasswordBox();
        private readonly bool isPassword;

        private InputDialog(string title, string question, string initial, bool isPassword)
        {
            this.isPassword = isPassword;
            Title = title;
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("PanelBrush");

            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(20);

            TextBlock label = new TextBlock();
            label.Text = question;
            label.TextWrapping = TextWrapping.Wrap;
            label.Margin = new Thickness(0, 0, 0, 10);
            panel.Children.Add(label);

            if (isPassword)
            {
                panel.Children.Add(passwordBox);
            }
            else
            {
                textBox.Text = initial ?? "";
                panel.Children.Add(textBox);
            }

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 16, 0, 0);

            Button ok = new Button();
            ok.Content = "ACEPTAR";
            ok.IsDefault = true;
            ok.Click += (s, e) => { DialogResult = true; };

            Button cancel = new Button();
            cancel.Content = "CANCELAR";
            cancel.IsCancel = true;
            cancel.Style = (Style)Application.Current.FindResource("GhostButton");
            cancel.Margin = new Thickness(0);

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;

            Loaded += (s, e) =>
            {
                if (isPassword)
                {
                    passwordBox.Focus();
                }
                else
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            };
        }

        public static string Ask(Window owner, string title, string question, string initial, bool isPassword)
        {
            InputDialog dialog = new InputDialog(title, question, initial, isPassword);
            dialog.Owner = owner;
            if (dialog.ShowDialog() == true)
            {
                return isPassword ? dialog.passwordBox.Password : dialog.textBox.Text.Trim();
            }
            return null;
        }
    }
}
