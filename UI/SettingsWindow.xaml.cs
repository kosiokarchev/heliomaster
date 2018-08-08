using System;
using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using heliomaster_wpf.Properties;
using Microsoft.Win32;

namespace heliomaster_wpf
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow() {
            InitializeComponent();

            RemotePasswordBox.Password = S.Remote.Pass;

            O.Dome.Connected += () => { fillCans(O.Dome.Driver, DomeCansPanel.Children); };
            O.Mount.Connected += () => { fillCans(O.Mount.Driver, MountCansPanel.Children); };
            O.Weather.Connected += () => { fillCans(O.Weather.Driver, WeatherCansPanel.Children); };
        }

        private static void fillCans(ASCOM.DriverAccess.AscomDriver driver, IList children) {
            children.Clear();
            foreach (var p in driver.GetType().GetProperties()) {
                if (p.PropertyType == typeof(bool) && p.Name.StartsWith("Can"))
                    children.Add(new CheckBox {
                        Content   = p.Name,
                        IsChecked = (bool) p.GetValue(driver)
                    });
            }
        }

        private void RemotePasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            S.Remote.Pass = RemotePasswordBox.Password;
        }

        private void BrowsePrivatekeyButton_Click(object sender, RoutedEventArgs e) {
            var fdialog = new OpenFileDialog {
                Multiselect      = false,
                CheckFileExists  = true,
                InitialDirectory = S.Remote.PrivateKeyFilename
            };
            if (fdialog.ShowDialog() == true)
                S.Remote.PrivateKeyFilename = fdialog.FileName;
        }

        private async void RemoteTestConnectionButton_Click(object sender, RoutedEventArgs e) {
            RemoteTestConnectionButton.Click -= RemoteTestConnectionButton_Click;
            RemoteTestConnectionButton.Content = "Connecting...";
            RemoteTestConnectionButton.IsEnabled = false;

            var res = await Task<bool>.Factory.StartNew(() => O.Default.ConnectRemote());

            RemoteTestConnectionButton.Click   += RemoteTestConnectionButton_Click;
            RemoteTestConnectionButton.Content =  "Test connection";
            RemoteTestConnectionButton.IsEnabled = true;

            MessageBox.Show(res ? "Connection successful!" : $"Connection failed:{Environment.NewLine}SSH: {O.Remote.SSHError?.Message}{Environment.NewLine}SFTP: {O.Remote.SFTPError?.Message}");
        }

        private void ChooseButton_Click(object sender, RoutedEventArgs e) {
            if ((sender as Button)?.DataContext is DomeSettings ds)
                ds.DomeID = ASCOM.DriverAccess.Dome.Choose(ds.DomeID);
            else if ((sender as Button)?.DataContext is MountSettings ms)
                ms.MountID = ASCOM.DriverAccess.Telescope.Choose(ms.MountID);
            else if ((sender as Button)?.DataContext is WeatherSettings ws)
                ws.WeatherID = ASCOM.DriverAccess.ObservingConditions.Choose(ws.WeatherID);
            else if ((sender as Button)?.DataContext is CameraModel cs) {
                if ((((Button) sender).Tag as string)?.ToLower() == "focuser")
                    cs.FocuserID = ASCOM.DriverAccess.Focuser.Choose(cs.FocuserID);
                else
                    cs.CameraID = ASCOM.DriverAccess.Camera.Choose(cs.CameraID);
            }
        }


        protected override void OnClosing(CancelEventArgs e) {
            e.Cancel = true;
            S.Save();
            Hide();
        }
    }
}
