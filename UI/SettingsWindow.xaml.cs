using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using heliomaster.Properties;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace heliomaster
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow() {
            InitializeComponent();

            RemotePasswordBox.Password = S.Remote.Pass;
            NetioPasswordBox.Password = S.Power.Netio.PassString;

            O.Dome.Connected += () => { fillCans(O.Dome.Driver, DomeCans); };
            O.Mount.Connected += () => { fillCans(O.Mount.Driver, MountCans); };
        }

        public ObservableConcurrentList<CheckBox> DomeCans { get; set; } = new ObservableConcurrentList<CheckBox>();
        public ObservableConcurrentList<CheckBox> MountCans { get; set; } = new ObservableConcurrentList<CheckBox>();

        private static void fillCans(object driver, ICollection<CheckBox> children) {
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
            S.Remote.Pass = RemotePasswordBox.Password; // TODO: Secure remote password
        }
        private void NetioPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            S.Power.Netio.Pass = NetioPasswordBox.SecurePassword;
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
            RemoteTestConnectionButton.Content = "Connecting...";
            RemoteTestConnectionButton.IsEnabled = false;

            MessageBox.Show(
                await Task<bool>.Factory.StartNew(() => O.Default.ConnectRemote())
                    ? "Connection successful!"
                    : $"Connection failed:{Environment.NewLine}SSH: {O.Remote.SSHError?.Message}{Environment.NewLine}SFTP: {O.Remote.SFTPError?.Message}");

            RemoteTestConnectionButton.Content =  "Test connection";
            RemoteTestConnectionButton.IsEnabled = true;


        }

        private async void NetioTestConnectionButton_Click(object sender, RoutedEventArgs e) {
            NetioTestConnectionButton.Content   =  "Connecting...";
            NetioTestConnectionButton.IsEnabled =  false;

            MessageBox.Show(await Task<bool>.Factory.StartNew(() => O.Power.Available) ? "Connection successful" : "Connection failed.");

            NetioTestConnectionButton.Content   =  "Test connection";
            NetioTestConnectionButton.IsEnabled =  true;

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

        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button b) {
                if (b.DataContext is LoggingSettings ls) {
                    var fdialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath        = ls.Directory,
                        Description         = "Select the base folder for Heliomaster logs."
                    };
                    if (fdialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        ls.Directory = fdialog.SelectedPath;
                } else if (b.DataContext is PythonSettings ps) {
                    var fdialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath        = ps.Path,
                        Description         = "Select a folder to add to PYTHONPATH."
                    };
                    if (fdialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        ps.Path = fdialog.SelectedPath;
                } else if (b.DataContext is WeatherSettings ws) {
                    var fdialog = new OpenFileDialog {
                        Multiselect      = false,
                        CheckFileExists  = true,
                        InitialDirectory = ws.FilePath
                    };
                    if (fdialog.ShowDialog() == true)
                        S.Weather.FilePath = fdialog.FileName;
                } else if (b.DataContext is RemoteSettings rs) {
                    var fdialog = new OpenFileDialog {
                        Multiselect      = false,
                        CheckFileExists  = true,
                        InitialDirectory = rs.PrivateKeyFilename
                    };
                    if (fdialog.ShowDialog() == true)
                        rs.PrivateKeyFilename = fdialog.FileName;
                }
            }
        }
    }
}
