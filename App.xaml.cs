using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using heliomaster_wpf.Properties;

namespace heliomaster_wpf {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public App() {}

        protected override void OnStartup(StartupEventArgs args) {
            base.OnStartup(args);

            SetupErrorHandling();

            Python.Initialize();

            QHYCCDCamera.initResource();

            O.StartRefresh(S.Settings.Refresh);

            try { // TODO: Smarter way to initialize connection
                if (O.Default.ConnectRemote()) {
                    if (S.Remote.DoInitCommand && !string.IsNullOrWhiteSpace(S.Remote.InitCommand)) {
                        var initcmdtask = O.Remote.Execute(S.Remote.InitCommand);
                        initcmdtask.Wait();
                        if (initcmdtask.Exception != null)
                            throw initcmdtask.Exception;
                        var initcmd = initcmdtask.Result;
                        if (!initcmd.Success)
                            MessageBox.Show($"Initialization script failed{Environment.NewLine}{initcmd.cmd.CommandText}{Environment.NewLine}Exit code: {initcmd.cmd.ExitStatus}");
                    }
                } else
                    MessageBox.Show($"Could not connect:{Environment.NewLine}SSH: {O.Remote.SSHError?.Message}{Environment.NewLine}SFTP: {O.Remote.SFTPError?.Message}");
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }

            if (S.Power.Netio.Get() != null) {
                S.Power.Netio.Register(O.Mount, S.Power.MountName);
                S.Power.Netio.Register(O.Dome, S.Power.DomeName);
            }
        }

        private void SetupErrorHandling() {
            TaskScheduler.UnobservedTaskException += (sender, args) => {
                MessageBox.Show($"Task exception: {args.Exception.InnerExceptions[0].Message}");
                args.SetObserved();
            };

            DispatcherUnhandledException += (sender, args) => {
                MessageBox.Show($"An exception occurred on the main UI thread:{Environment.NewLine}"
                                + $"{args.Exception.GetType().Name}: {args.Exception.Message}");
                args.Handled = true;
            };
        }
    }
}
