using System;
using System.Collections.Generic;
using System.Windows;
using heliomaster.Properties;
using PyR = Python.Runtime;

namespace heliomaster {
    public class NoPythonException : NotSupportedException {
        public NoPythonException() : base("No Py support.") { }
    }

    public static class Py {
        public static dynamic np;
        public static dynamic ephem;
        public static dynamic logger;
        public static dynamic lib;

        public static bool Running;

        public static void Initialize() {
            // TODO: On restarting crashes with segfault => disable rebooting.
            if (S.Python.IsEnabled) Start();

            S.Python.PropertyChanged += (s, args) => {
                if (args.PropertyName == nameof(S.Python.IsEnabled)) {
                    if (S.Python.IsEnabled && !Running)      Start();
                    else if (!S.Python.IsEnabled && Running) Stop();
                }
            };
        }

        public static void Start() {
            try {
                var t = DateTime.Now;
                PyR.PythonEngine.Initialize();
                PyR.PythonEngine.BeginAllowThreads();
                Running = true;

                Run(() => {
                    if (!string.IsNullOrWhiteSpace(S.Python.Path)) {
                        dynamic sys = PyR.Py.Import("sys");
                        sys.path.append(S.Python.Path);
                    }

                    lib = PyR.Py.Import("libhm");

                    logger = ((dynamic) PyR.Py.Import("logger")).setup(
                        string.IsNullOrWhiteSpace(S.Logging.Directory)
                            ? "." : S.Logging.Directory,
                        S.Logging.Debug, S.Logging.Info, S.Logging.Error);

                    np    = PyR.Py.Import("numpy");
                    ephem = PyR.Py.Import("ephem");
                    Pynder.PyObjects.Initialize();
                });

                Logger.debug($"Py started in {(DateTime.Now - t).TotalSeconds}s");
            } catch (Exception e) {
                MessageBox.Show($"Starting Py failed: {e.Message}");
            }
        }

        public static void Stop() {
            try {
                PyR.PythonEngine.Shutdown();
            } catch {}

            Running = false;

            np = null;
            ephem = null;
            Pynder.PyObjects.Initialize();
        }

        public static void Run(Action a) {
            using (PyR.Py.GIL()) {
                try {
                    a();
                } catch (PyR.PythonException) {
                    throw;
                } catch {} // TODO: Better python error handling, although it's hard
            }
        }
    }

    public static class Pynder {
        public static double rad2hours   = 12 / Math.PI;
        public static double rad2degrees = 180 / Math.PI;

        public static class PyObjects {
            public static dynamic Sun;
            public static dynamic Moon;

            public static void Initialize() {
                if (Py.Running) {
                    Sun  = Py.ephem.Sun;
                    Moon = Py.ephem.Moon;
                } else {
                    Sun = null;
                    Moon = null;
                }
            }
        }

        public struct Coords {
            public double ra, dec;
        }

        public enum Objects {
            Sun, Moon
        }
        public static readonly Dictionary<Objects, dynamic> ObjectPyObjects = new Dictionary<Objects, dynamic> {
            {Objects.Sun, PyObjects.Sun},
            {Objects.Moon, PyObjects.Moon}
        };

        private static dynamic _obs;
        public static dynamic obs {
            get {
                if (_obs == null) Py.Run(() => { _obs = Py.ephem.city("Madrid"); });
                return _obs;
            }
        }

        public static Coords find(Objects obj) {
            if (!Py.Running)
                throw new NoPythonException();

            var ret = new Coords();
            Py.Run(() => {
                obs.date = Py.ephem.now();
                var data = ObjectPyObjects[obj](obs);
                ret.ra  = data.ra  * rad2hours;
                ret.dec = data.dec * rad2degrees;
            });
            return ret;
        }
    }
}
