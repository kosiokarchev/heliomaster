using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using heliomaster_wpf.Properties;

namespace heliomaster_wpf {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public App() {
            O.StartRefresh(S.Settings.Refresh);

            Python.Start();

//            O.Default.ConnectRemote();
        }
    }
}
