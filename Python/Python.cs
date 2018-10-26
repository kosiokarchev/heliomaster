using System;
using System.Windows;
using heliomaster.Properties;
using PyR = Python.Runtime;

namespace heliomaster {
    /// <summary>
    /// An exception that signifies that Python cannot be loaded and used.
    /// </summary>
    public class NoPythonException : NotSupportedException {
        public NoPythonException() : base("No Python support.") { }
    }

    /// <summary>
    /// A class that interfaces with Python and the Heliomaster Python library (HMPL) through <see cref="PyR"/>.
    /// </summary>
    /// <remarks>After the interpreter is started through <see cref="Start"/> (or automatically from
    /// <see cref="Initialize"/>), any code that accesses Python objects or functions should be wrapped in an
    /// interpreter lock capture through <see cref="PyR.Py.GIL()"/>. This class defines utility methods
    /// <see cref="Run(Action)"/> and <see cref="Run(System.Func{dynamic})"/> which executes C# code within a <c>using
    /// (PyR.Py.GIL())</c> block and makes an attempt at exception handling.</remarks>
    public static class Py {
        /// <summary>
        /// <a href="http://www.numpy.org">NumPy</a>.
        /// </summary>
        public static dynamic np;
        
        /// <summary>
        /// <a href="https://rhodesmill.org/pyephem/">PyEphem</a>.
        /// </summary>
        public static dynamic ephem;
        
        /// <summary>
        /// The Heliomaster Python library (HMPL).
        /// </summary>
        public static dynamic lib;
        
        /// <summary>
        /// The logger module of HMPL.
        /// </summary>
        public static dynamic logger;
        
        /// <summary>
        /// The detect_body function of HMPL.
        /// </summary>
        public static dynamic detect_body;

        /// <summary>
        /// Whether the <see cref="PyR.PythonEngine"/> is running.
        /// </summary>
        public static bool Running;

        /// <summary>
        /// Setup the class - register events to start and stop the Python interpreter when the
        /// <see cref="PythonSettings.IsEnabled"/> property of <see cref="S.Python"/> changes.
        /// </summary>
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

        /// <summary>
        /// Start the Python interpreter (<see cref="PyR.PythonEngine.Initialize()"/>) and load the necessary modules.
        /// </summary>
        /// <remarks>
        /// <list type="number">
        ///     <item>
        ///         <description>
        ///             Appends <see cref="PythonSettings.Path"/> to <c>sys.path</c> and Loads the following:
        ///             <list type="bullet">
        ///                 <item><term>libhm - <see cref="lib"/></term></item>
        ///                 <item><term>numpy - <see cref="np"/></term></item>
        ///                 <item><term>ephem - <see cref="ephem"/></term></item>
        ///             </list>
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <description>Sets up the Python logger from <c>libhm.logger</c> by <c>libhm.logger_setup</c> and
        ///         saves it in <see cref="logger"/>.</description>
        ///     </item>
        ///     <item>
        ///         <description>Initialises <see cref="Pynder.PyEphemObjects"/>.</description>
        ///     </item>
        /// </list>
        /// If an error occurs, a <see cref="MessageBox"/> is shown notifying the user that Python has not been initialised.
        /// </remarks>
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

                    logger = lib.logger_setup(
                        string.IsNullOrWhiteSpace(S.Logging.Directory)
                            ? "." : S.Logging.Directory,
                        S.Logging.Debug, S.Logging.Info, S.Logging.Error);

                    detect_body = lib.detect_planet;

                    np    = PyR.Py.Import("numpy");
                    ephem = PyR.Py.Import("ephem");
                    Pynder.PyEphemObjects.Initialize();
                });

                Logger.debug($"Python started in {(DateTime.Now - t).TotalSeconds}s");
            } catch (Exception e) {
                MessageBox.Show($"Starting Py failed: {e.Message}");
            }
        }

        /// <summary>
        /// Shut down the Python interpreter (<see cref="PyR.PythonEngine.Shutdown"/>).
        /// </summary>
        public static void Stop() {
            try { PyR.PythonEngine.Shutdown(); } catch {}

            Running = false;

            np = null;
            ephem = null;
            Pynder.PyEphemObjects.Initialize();
        }

        /// <summary>
        /// Execute code that needs the Python Global Interpreter Lock.
        /// </summary>
        /// <param name="a">An <see cref="Action"/> containing the python-accessing code.</param>
        public static void Run(Action a) {
            using (PyR.Py.GIL()) {
                try {
                    a();
                } catch (PyR.PythonException) {
                    throw;
                } catch {} // TODO: Better python error handling, although it's hard
            }
        }

        /// <summary>
        /// Execute code that needs the Python Global Interpreter Lock and returns a value.
        /// </summary>
        /// <param name="a">A <see cref="Func{TResult}"/> containing the python-accessing code and returning a Python
        /// object.</param>
        public static dynamic Run(Func<dynamic> a) {
            dynamic ret = null;
            Run(() => {
                ret = a();
            });
            return ret;
        }
    }
}
