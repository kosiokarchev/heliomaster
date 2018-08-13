using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using heliomaster_wpf.Properties;

namespace heliomaster_wpf {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public App() {
            QHYCCDCamera.initResource();
            foreach (var iD in QHYCCDCamera.CameraIDs) {
                Console.WriteLine(iD);
            }

            O.StartRefresh(S.Settings.Refresh);

            if (S.Python.IsEnabled)
                Python.Start();

            try {
                O.Default.ConnectRemote();
            } catch {} // TODO: Smarter way to initialize connection

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
