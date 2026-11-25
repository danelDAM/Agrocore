using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Views
{
    /// <summary>Erabiltzaileen taulako errenkada.</summary>
    public class UserRow
    {
        public UserDto User { get; set; }
        public string Username { get { return User.Username; } }
        public string FullName { get { return User.FullName; } }
        public string RoleText { get { return Texts.Role(User.Role); } }
        public string FarmText { get { return User.Role == UserRole.Admin ? "(guztiak)" : User.FarmName; } }
        public string ActiveText { get { return User.IsActive ? "Aktiboa" : "Ez-aktiboa"; } }
        public string ToggleText { get { return User.IsActive ? "Desaktibatu" : "Aktibatu"; } }
        public string LastLoginText { get { return User.LastLogin.HasValue ? User.LastLogin.Value.ToString("dd/MM HH:mm") : "—"; } }
    }

    /// <summary>
    /// Administrazioa (Administratzaile rola bakarrik; zerbitzariak berriro egiaztatzen du):
    /// nekazarien alta (User + Farm sortzen ditu), operadoreak eta behatzaileak, edizioa, aktibatu/desaktibatu,
    /// ezabatu, pasahitza aldatu, flota-gakoa berriro sortu eta sistemaren konfigurazioa.
    /// </summary>
    public partial class AdminPage : UserControl
    {
        private readonly MainWindow main;
        private SystemConfig config;

        public AdminPage(MainWindow main)
        {
            InitializeComponent();
            this.main = main;
            main.MessageArrived += OnMessage;
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    main.Send(new ListUsersRequest());
                    main.Send(new GetConfigRequest());
                }
            };
        }

        private Window Owner
        {
            get { return Window.GetWindow(this); }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            main.Send(new ListUsersRequest());
        }

        // ==================================================================
        // ZERBITZARIAREN ERANTZUNAK
        // ==================================================================

        private void OnMessage(string type, string json)
        {
            switch (type)
            {
                case MessageTypes.UsersResponse:
                    ListUsersResponse users = MessageSerializer.FromJson<ListUsersResponse>(json);
                    ShowUsers(users);
                    break;

                case MessageTypes.CreateFarmerResponse:
                    CreateFarmerResponse farmer = MessageSerializer.FromJson<CreateFarmerResponse>(json);
                    if (farmer.Success)
                    {
                        MessageBox.Show(Owner,
                            farmer.Message + "\n\nFlota-gakoa: " + farmer.FleetKey + "\n\n" +
                            "Gorde ezazu: makinek behar dute konektatzeko eta ez da berriro erakutsiko.\n\n" +
                            "Flota abiarazteko:\nAgroCore.MachineClient --fleet --farm " + farmer.FarmId + " --key " + farmer.FleetKey,
                            "Flota-gakoa", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearFarmerForm();
                        main.Send(new ListUsersRequest());
                        main.RefreshFarmList();
                    }
                    else
                    {
                        MessageBox.Show(Owner, farmer.Message, "Ezin izan da osatu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    break;

                case MessageTypes.ConfigResponse:
                    ConfigResponse configResponse = MessageSerializer.FromJson<ConfigResponse>(json);
                    if (configResponse.Config != null)
                    {
                        ShowConfig(configResponse.Config);
                    }
                    break;

                case MessageTypes.Result:
                    OperationResult result = MessageSerializer.FromJson<OperationResult>(json);
                    OnResult(result);
                    break;
            }
        }

        private void OnResult(OperationResult result)
        {
            string type = result.RequestType;
            if (type == MessageTypes.CreateUser || type == MessageTypes.UpdateUser ||
                type == MessageTypes.SetUserActive || type == MessageTypes.DeleteUser)
            {
                if (!result.Success)
                {
                    MessageBox.Show(Owner, result.Message, "Ezin izan da osatu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (type == MessageTypes.CreateUser)
                {
                    UserName.Clear();
                    UserLogin.Clear();
                    UserPassword.Clear();
                }
                main.Send(new ListUsersRequest());
                if (type == MessageTypes.DeleteUser || type == MessageTypes.UpdateUser)
                {
                    main.RefreshFarmList();
                }
            }
            else if (type == MessageTypes.UpdateConfig)
            {
                ConfigResult.Text = result.Message;
            }
        }

        private void ShowUsers(ListUsersResponse response)
        {
            List<UserRow> rows = new List<UserRow>();
            foreach (UserDto user in response.Users)
            {
                UserRow row = new UserRow();
                row.User = user;
                rows.Add(row);
            }
            UsersGrid.ItemsSource = rows;
            FarmsGrid.ItemsSource = response.Farms;

            // Ustiategiak operadoreen/behatzaileen altarako
            UserFarmBox.Items.Clear();
            foreach (FarmDto farm in response.Farms)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = farm.Name;
                item.Tag = farm.Id;
                UserFarmBox.Items.Add(item);
            }
            if (UserFarmBox.Items.Count > 0)
            {
                UserFarmBox.SelectedIndex = 0;
            }
        }

        // ==================================================================
        // ERABILTZAILEEN GAINEKO EKINTZAK
        // ==================================================================

        private UserDto UserOf(object sender)
        {
            return ((UserRow)((Button)sender).DataContext).User;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            UserDto user = UserOf(sender);
            string name = InputDialog.Ask(Owner, "Erabiltzailea editatu", user.Username + " erabiltzailearen izen osoa:", user.FullName, false);
            if (name == null)
            {
                return;
            }

            UpdateUserRequest request = new UpdateUserRequest();
            request.UserId = user.Id;
            request.FullName = name;

            if (user.Role == UserRole.Farmer)
            {
                string farm = InputDialog.Ask(Owner, "Ustiategia editatu", "Ustiategiaren izena:", user.FarmName, false);
                if (farm == null)
                {
                    return;
                }
                request.FarmName = farm;
            }
            main.Send(request);
        }

        private void Password_Click(object sender, RoutedEventArgs e)
        {
            UserDto user = UserOf(sender);
            string password = InputDialog.Ask(Owner, "Pasahitz berria", user.Username + " erabiltzailearen pasahitz berria (gutxienez 6, letrak eta zenbakiak):", "", true);
            if (string.IsNullOrEmpty(password))
            {
                return;
            }
            UpdateUserRequest request = new UpdateUserRequest();
            request.UserId = user.Id;
            request.NewPassword = password;
            main.Send(request);
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            UserDto user = UserOf(sender);
            SetUserActiveRequest request = new SetUserActiveRequest();
            request.UserId = user.Id;
            request.Active = !user.IsActive;
            main.Send(request);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            UserDto user = UserOf(sender);
            string warning = user.Username + " erabiltzailea ezabatu?";
            if (user.Role == UserRole.Farmer)
            {
                warning = warning + "\n\nNekazaria denez, bere \"" + user.FarmName
                          + "\" ustiategia ere ezabatuko da, lotutako soro, makina eta erabiltzaile guztiekin.";
            }
            if (MessageBox.Show(Owner, warning + "\n\nEkintza hau ezin da desegin.", "Erabiltzailea ezabatu",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
            DeleteUserRequest request = new DeleteUserRequest();
            request.UserId = user.Id;
            main.Send(request);
        }

        private void RegenerateKey_Click(object sender, RoutedEventArgs e)
        {
            FarmDto farm = (FarmDto)((Button)sender).DataContext;
            if (MessageBox.Show(Owner, farm.Name + " ustiategirako flota-gako berria sortu?\n" +
                                "Gako zaharra erabiltzen duten makinak ezin izango dira konektatu.",
                                "Flota-gakoa", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            RegenerateFleetKeyRequest request = new RegenerateFleetKeyRequest();
            request.FarmId = farm.Id;
            main.Send(request);
        }

        // ==================================================================
        // ALTAK
        // ==================================================================

        private void CreateFarmer_Click(object sender, RoutedEventArgs e)
        {
            CreateFarmerRequest request = new CreateFarmerRequest();
            request.FullName = FarmerName.Text.Trim();
            request.Username = FarmerUser.Text.Trim();
            request.Password = FarmerPassword.Password;
            request.FarmName = FarmName.Text.Trim();

            int fields, chargers, seeders, irrigators, sprayers, harvesters;
            if (!int.TryParse(FieldsBox.Text, out fields) || !int.TryParse(ChargersBox.Text, out chargers) ||
                !int.TryParse(SeedersBox.Text, out seeders) || !int.TryParse(IrrigatorsBox.Text, out irrigators) ||
                !int.TryParse(SprayersBox.Text, out sprayers) || !int.TryParse(HarvestersBox.Text, out harvesters))
            {
                MessageBox.Show(Owner, "Soro, kargagailu eta makinen kopuruak zenbaki osoak izan behar dute.", "Datu okerrak");
                return;
            }
            request.FieldCount = fields;
            request.Chargers = chargers;
            request.Seeders = seeders;
            request.Irrigators = irrigators;
            request.Sprayers = sprayers;
            request.Harvesters = harvesters;
            main.Send(request);
        }

        private void ClearFarmerForm()
        {
            FarmerName.Clear();
            FarmerUser.Clear();
            FarmerPassword.Clear();
            FarmName.Clear();
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            CreateUserRequest request = new CreateUserRequest();
            request.FullName = UserName.Text.Trim();
            request.Username = UserLogin.Text.Trim();
            request.Password = UserPassword.Password;
            if (UserRoleBox.SelectedIndex == 0) request.Role = UserRole.Operator;
            else if (UserRoleBox.SelectedIndex == 1) request.Role = UserRole.Viewer;
            else request.Role = UserRole.Admin;

            ComboBoxItem farm = UserFarmBox.SelectedItem as ComboBoxItem;
            request.FarmId = farm != null ? (int)farm.Tag : 0;
            main.Send(request);
        }

        // ==================================================================
        // KONFIGURAZIOA
        // ==================================================================

        private void ShowConfig(SystemConfig newConfig)
        {
            config = newConfig;
            foreach (CropDefinition crop in config.Crops)
            {
                string minutes = crop.GrowthMinutes.ToString(CultureInfo.CurrentCulture);
                if (crop.Type == CropType.Carrot) CarrotBox.Text = minutes;
                if (crop.Type == CropType.Corn) CornBox.Text = minutes;
                if (crop.Type == CropType.Tomato) TomatoBox.Text = minutes;
            }
            ResetBox.Text = config.HarvestedResetSeconds.ToString();
            LowBatteryBox.Text = config.Battery.LowBatteryThreshold.ToString(CultureInfo.CurrentCulture);
            ChargeRateBox.Text = config.Battery.ChargeRatePerSecond.ToString(CultureInfo.CurrentCulture);
            AttemptsBox.Text = config.MaxLoginAttempts.ToString();
            LockoutBox.Text = config.LockoutSeconds.ToString();
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (config == null)
            {
                return;
            }

            double carrot = 0, corn = 0, tomato = 0, lowBattery = 0, chargeRate = 0;
            int reset = 0, attempts = 0, lockout = 0;
            bool ok = double.TryParse(CarrotBox.Text, out carrot) && double.TryParse(CornBox.Text, out corn)
                      && double.TryParse(TomatoBox.Text, out tomato) && double.TryParse(LowBatteryBox.Text, out lowBattery)
                      && double.TryParse(ChargeRateBox.Text, out chargeRate) && int.TryParse(ResetBox.Text, out reset)
                      && int.TryParse(AttemptsBox.Text, out attempts) && int.TryParse(LockoutBox.Text, out lockout);
            if (!ok || carrot <= 0 || corn <= 0 || tomato <= 0 || chargeRate <= 0 || lowBattery < 5 || lowBattery > 60 || attempts < 1)
            {
                ConfigResult.Text = "Berrikusi balioak (zenbaki positiboak; bateria baxua 5 eta 60 artean).";
                return;
            }

            foreach (CropDefinition crop in config.Crops)
            {
                if (crop.Type == CropType.Carrot) crop.GrowthMinutes = carrot;
                if (crop.Type == CropType.Corn) crop.GrowthMinutes = corn;
                if (crop.Type == CropType.Tomato) crop.GrowthMinutes = tomato;
            }
            config.HarvestedResetSeconds = reset;
            config.Battery.LowBatteryThreshold = lowBattery;
            config.Battery.ChargeRatePerSecond = chargeRate;
            config.MaxLoginAttempts = attempts;
            config.LockoutSeconds = lockout;

            UpdateConfigRequest request = new UpdateConfigRequest();
            request.Config = config;
            main.Send(request);
        }
    }
}
