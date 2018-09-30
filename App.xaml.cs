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
using heliomaster.Properties;

namespace heliomaster {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public App() {}

        protected override void OnStartup(StartupEventArgs args) {
            base.OnStartup(args);

            SetupErrorHandling();

            Py.Initialize();

            // QHYCCDCamera.initResource();

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




//            var w = (Weather) new WeatherFromFile();
//            var res = await w.Connect(@"C:\Users\Kosio\Desktop\AAG_CCDAP4.dat");
//            var b = 314;
        }

        private void SetupErrorHandling() {
            // TODO: Better error handling:
            // e.g. generate a meaningful message from internal exceptions, etc.

            TaskScheduler.UnobservedTaskException += (sender, args) => {
                Logger.error($"Task exception: {args.Exception.InnerExceptions[0].Message}");
                args.SetObserved();
            };

            DispatcherUnhandledException += (sender, args) => {
                Logger.error($"An exception occurred on the main UI thread:{Environment.NewLine}"
                             + $"{args.Exception.GetType().Name}: {args.Exception.Message}");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                var msg = $"{args.ExceptionObject.GetType().Name}: {(args.ExceptionObject is Exception e ? e.Message : args.ExceptionObject.ToString())}";
                if (args.IsTerminating)
                    Logger.critical(msg);
                else
                    Logger.error(msg);
            };
        }
    }
}
