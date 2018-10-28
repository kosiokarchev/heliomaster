using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        public bool CanStart => AutomationState == AutomationStates.Idle;

        public bool CanSetShutdown =>
            AutomationState == AutomationStates.Idle || AutomationState == AutomationStates.InOperation;

        public  bool      CanShutdown { get; } = true;
        private DateTime? _shutdownTime;

        public DateTime? ShutdownTime {
            get => _shutdownTime;
            set {
                if (_shutdownTime.Equals(value)) return;
                _shutdownTime = value;
                OnPropertyChanged();
            }
        }

        private CancellationTokenSource closewait;
        private Task                    closewaittask;

        public async void SetShutdown(DateTime st) {
            await Interrupt();
            closewaittask = Task.Run(() => {
                closewait    = new CancellationTokenSource();
                ShutdownTime = st;
                try {
                    Inform($"Setting shutdown timer for {st}.");
                    Task.Delay(st - DateTime.Now, closewait.Token).Wait();
                    Inform($"Shutdown timer has expired at {DateTime.Now}");
                    ShuttingRaise();
                } catch (Exception e) {
                    Inform("Shutdown timer has been aborted.");
                    Logger.debug($"Exception during wait for shutdown: {Utilities.FormatException(e)}");
                }

                ShutdownTime  = null;
                closewait     = null;
                closewaittask = null;
            });
        }

        public async Task Interrupt() {
            closewait?.Cancel();

            if (closewaittask != null)
                try {
                    if (await Task.WhenAny(closewaittask, Task.Delay(TimeSpan.FromSeconds(1))) != closewaittask
                    ) // TODO: Unhardcode, maybe better way to wait...
                        Logger.debug("Could not stop closewaittask in time.");
                    closewaittask.Dispose();
                } catch (Exception e) {
                    Logger.debug($"Error while waiting for closewaittask to finish: {Utilities.FormatException(e)}");
                }

            ShutdownTime  = null;
            closewait     = null;
            closewaittask = null;
        }

        private readonly SemaphoreSlim autosem = new SemaphoreSlim(1, 1);

        public async Task<bool> Startup(StartupArguments args) {
            await autosem.WaitAsync();

            try {
                if (AutomationState != AutomationStates.Idle)
                    throw new RefuseAutomationWarning("The state should be idle for startup");
                AutomationState = AutomationStates.Starting;

                Inform("Connecting devices.");
                await connect(Mount);
                await connect(Dome);
                await connect(Weather);

                Inform("Starting weather protection.");
                if (!StartWeatherProtection()) throw new RefuseAutomationWarning("Good weather not confirmed.");

                Inform("Opening dome.");
                if (!await Dome.SmartShutter(true)) throw new AutoOperationsException("Could not open dome.");

                Inform("Pointing telescope.");
                if (!(await Mount.Unpark() && await Mount.GoTo()))
                    throw new AutoOperationsException("Could not operate telescope.");

                Inform("Syncing dome.");
                await SlaveDomeToMount();


                Inform("Starting cameras.");
                foreach (var model in CameraModels)
                    model.Cam.StartLivePreview(30); // TODO: Unhardcode maxfps --> cam setting

                CommonTimelapse.TieAll();
                if (CommonTimelapse.Main is Timelapse m) {
                    if (args.CamShutdownTime is DateTime cst) {
                        m.StopMethod = 2;
                        m.End        = cst;
                    } else { } // TODO: What to do when no end time given? Until object sets?
                }

                Inform(await ObjectIsInView() ? "The Sun is in view." : "The Sun is not in view!");
                if (args.RequireInView && !SearchForObject()) throw new ObjectNotLocatedError();


                if (args.Autostart) {
                    Inform("Starting timelapses.");
                    CommonTimelapse.Start(CameraModel.TimelapseAction, CameraModels);
                }

                AutomationState = AutomationStates.InOperation;
                StartupSuccessRaise();

                if (args.ShutdownTime is DateTime st) SetShutdown(st);

                return true;
            } catch (ObservatoryWarning w) {
                Emit(w);
                AutomationState = AutomationStates.Idle;
                return false;
            } catch (ObservatoryException e) {
                Emit(e);
                StartupFailureRaise(e);
                AutomationState = AutomationStates.Faulted;
                FixingRaise();
                return false;
            } finally {
                autosem.Release();
            }
        }

        public async Task<bool> Shutdown() {
            await autosem.WaitAsync();
            try {
                await Interrupt();
                AutomationState = AutomationStates.Closing;

                await connect(Mount);
                await connect(Dome);

                Inform("Stopping weather protection.");
                StopWeatherProtection();

                CommonTimelapse.Stop();

                var camtasks = CameraModels.Select(i => i.Cam.Stop());

                Inform("Unsyncing dome.");
                await UnSlaveDomeFromMount();

                var mounttask = Mount.Park();
                var dometask  = Dome.SmartShutter(false);

                Inform("Waiting for telescope to park.");
                if (!await mounttask) Emit(new AutoOperationsException("Could not park mount."));

                Inform("Waiting for dome to close.");
                if (!await dometask) Emit(new AutoOperationsException("Could not close dome."));

                Inform("Waiting for cameras ");
                foreach (var camtask in camtasks) await camtask;

                if (mounttask.Result && dometask.Result) {
                    Inform("Waiting for dome to park.");
                    await Dome.HomeOrPark(home: false);

                    AutomationState = AutomationStates.Idle;
                    ShutdownSuccessRaise();
                    return true;
                } else
                    throw new AutoOperationsException("Observatory shutdown with errors.");
            } catch (Exception e) {
                if (e is ObservatoryException) Emit(e as ObservatoryException);
                ShutdownFailureRaise(e);
                AutomationState = AutomationStates.Faulted;
                FixingRaise();
                return false;
            } finally {
                autosem.Release();
            }
        }

        public async Task<bool> Fix() {
            AutomationState = AutomationStates.Fixing;
            Logger.info("Attempting to fix observatory state.");

            bool domesuccess = false, mountsuccess = false;
            try {
                await connect(Dome);
                domesuccess = await Dome.SmartShutter(false);
                if (!domesuccess)
                    throw new FixingFailedError("Dome state could not be fixed.");
                else if (!await Dome.HomeOrPark(false)) Emit(new AutoOperationsWarning("Could not park dome."));
            } catch (Exception e) {
                Emit(new AutoOperationsException($"Error while fixing dome: {Utilities.FormatException(e)}"));
            }

            try {
                await connect(Mount);
                mountsuccess = await Mount.Park();
                if (!mountsuccess) throw new FixingFailedError("Mount state could not be fixed.");
            } catch (Exception e) {
                Emit(new AutoOperationsException($"Error while fixing mount: {Utilities.FormatException(e)}"));
            }

            if (!(domesuccess && mountsuccess)) {
                Emit(new CriticalObservatoryError("Could not fix observatory state"));
                FixingFailureRaise();
                AutomationState = AutomationStates.Faulted;
                return false;
            } else {
                Logger.info("State was successfully fixed.");
                AutomationState = AutomationStates.Idle;
                FixingSuccessRaise();
                return true;
            }
        }
    }
}
