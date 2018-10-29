using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace heliomaster {
    public partial class Observatory {
        /// <summary>
        /// Whether the observatory is currently in the <see cref="AutomationStates.Idle"/> state, and automation can be started.
        /// </summary>
        public bool CanStart => AutomationState == AutomationStates.Idle;

        /// <summary>
        /// Whether the observatory is currently in the <see cref="AutomationStates.Idle"/> or
        /// <see cref="AutomationStates.InOperation"/> state, and a shutdown can be scheduled.
        /// </summary>
        public bool CanSetShutdown =>
            AutomationState == AutomationStates.Idle || AutomationState == AutomationStates.InOperation;

        /// <summary>
        /// Whether a shutdown can be triggered now. Always true.
        /// </summary>
        public bool CanShutdown { get; } = true;
        
        private DateTime? _shutdownTime;
        /// <summary>
        /// The time a shutdown has been scheduled for
        /// </summary>
        public DateTime? ShutdownTime {
            get => _shutdownTime;
            set {
                if (_shutdownTime.Equals(value)) return;
                _shutdownTime = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// A token source used to stop <see cref="closewaittask"/> prematurely by <see cref="Interrupt"/>.
        /// </summary>
        private CancellationTokenSource closewait;
        /// <summary>
        /// A <see cref="Task"/> run by <see cref="SetShutdown"/> to wait until the scheduled <see cref="ShutdownTime"/>
        /// and raise <see cref="Shutting"/>.
        /// </summary>
        private Task closewaittask;

        /// <summary>
        /// Schedule a shutdown for <paramref name="time"/>.
        /// </summary>
        /// <param name="time">The time to schedule a shutdown for.</param>
        /// <remarks>Runs a <see cref="Task"/> (saved in <see cref="closewaittask"/>) that executes
        /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> with the time remaining until <paramref name="time"/>,
        /// waits for it to complete, and raises <see cref="Shutting"/>. If the delay is interrupted by cancelling
        /// <see cref="closewait"/>, an exception is thrown, and the timer is instead aborted without raising the
        /// event. During the waiting period <see cref="ShutdownTime"/> reflects <see cref="time"/>.</remarks>
        public async void SetShutdown(DateTime time) {
            await Interrupt();
            closewaittask = Task.Run(() => {
                closewait    = new CancellationTokenSource();
                ShutdownTime = time;
                try {
                    Inform($"Setting shutdown timer for {time}.");
                    Task.Delay(time - DateTime.Now, closewait.Token).Wait();
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

        /// <summary>
        /// Cancel any scheduled shutdown.
        /// </summary>
        /// <remarks>Triggers <see cref="closewait"/>, which cancels the delay in <see cref="closewaittask"/>.
        /// This might of course fail if called too close to the end of the wait.</remarks>
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

        /// <summary>
        /// A semaphore which allows only one automation operation to be performed at a time.
        /// </summary>
        private readonly SemaphoreSlim autosem = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initialise observatory automation. Triggered by the <see cref="Starting"/> event.
        /// </summary>
        /// <param name="args">Refer to <see cref="StartupArguments"/> for the parameters which control the startup
        ///     procedure.</param>
        /// <returns>Whether observatory automation has been successfully initialised.</returns>
        ///
        /// <remarks>
        /// <para>
        /// The startup procedure is as follows:
        /// <list type="number">
        ///     <item>
        ///         Check the <see cref="AutomationState"/> is <see cref="AutomationStates.Idle"/>.
        ///         <list type="bullet">
        ///             <item>If not, raise a <see cref="RefuseAutomationWarning"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>Transition to <see cref="AutomationStates.Starting"/>.</item>
        ///     <item>
        ///         Connect to the <see cref="Mount"/>, <see cref="Dome"/>, and <see cref="Weather"/>.
        ///         <list type="bullet">
        ///            <item>If any of the connections fail, raise <see cref="ConnectionError"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>
        ///         <see cref="StartWeatherProtection"/>.
        ///         <list type="bullet">
        ///             <item>If it returns <c>false</c> and the weather state is not safe, raise
        ///                 <see cref="RefuseAutomationWarning"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>
        ///         Open the <see cref="Dome"/> shutter via <see cref="heliomaster.Dome.SmartShutter"/>.
        ///         <list type="bullet">
        ///             <item>If this fails, raise <see cref="AutoOperationsException"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>
        ///         <see cref="heliomaster.Telescope.Unpark"/> the <see cref="Mount"/> and point it to the target of
        ///         observations via <see cref="heliomaster.Telescope.GoTo"/>.
        ///         <list type="bullet">
        ///             <item>If any of those fails, raise <see cref="AutoOperationsException"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item><see cref="SlaveDomeToMount"/></item>
        ///     <item><see cref="BaseCamera.StartLivePreview"/> of the connected cameras.</item>
        ///     <item><see cref="heliomaster.CommonTimelapse.TieAll"/> timelapses and set their
        ///         <see cref="Timelapse.End"/> to the <see cref="StartupArguments.CamShutdownTime"/> of
        ///         <paramref name="args"/> if it is not <c>null</c>.</item>
        ///     <item>
        ///         If <see cref="StartupArguments.RequireInView"/> is set in <paramref name="args"/>,
        ///         <see cref="SearchForObject"/>.
        ///         <list type="bullet">
        ///             <item>If this fails, raise <see cref="ObjectNotLocatedError"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>
        ///         If <see cref="StartupArguments.Autostart"/> is set in <paramref name="args"/>, start the timelapses.
        ///         <list type="bullet">
        ///             <item>If this fails, raise <see cref="AutoOperationsException"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>Transition to <see cref="AutomationStates.InOperation"/> and raise <see cref="StartupSuccess"/>.</item>
        ///     <item>If <see cref="StartupArguments.ShutdownTime"/> is set in <paramref name="args"/> <see cref="SetShutdown"/>.</item>
        /// </list>
        /// </para>
        /// 
        /// <para>
        /// The procedure might be interrupted by an <see cref="ObservatoryException"/>:
        /// <list type="bullet">
        ///     <item>If a <see cref="ObservatoryWarning"/> is thrown in the above procedure (the
        ///         <see cref="AutomationState"/> is not <see cref="AutomationStates.Idle"/> ot the weather state is not
        ///         safe), revert to <see cref="AutomationStates.Idle"/> taking no further actions, assuming the state
        ///         of the observatory has not been altered by the startup procedure.</item>
        ///     <item>If a non-warning <see cref="ObservatoryException"/> is thrown, raise <see cref="StartupFailure"/>,
        ///         transition to <see cref="AutomationStates.Faulted"/>, and raise <see cref="Fixing"/>.</item>
        /// </list>
        /// In both cases, return <c>false</c>. Otherwise, return <c>true</c>.
        /// </para>
        ///
        /// <para>All exceptions thrown in this method are caught an <see cref="Emit"/>-ted, instead of propagated up
        /// the call stack.</para>
        /// </remarks>
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
                    model.Cam.StartLivePreview(5); // TODO: Unhardcode maxfps --> cam setting

                CommonTimelapse.TieAll();
                if (CommonTimelapse.Main is Timelapse m) {
                    if (args.CamShutdownTime is DateTime cst) {
                        m.StopMethod = 2;
                        m.End        = cst;
                    } else { } // TODO: What to do when no end time given? Until object sets?
                }

                // TODO: Object detected?
                Inform(await ObjectIsInView() ? "The Sun is in view." : "The Sun is not in view!");
                if (args.RequireInView && !await SearchForObject()) throw new ObjectNotLocatedError();


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

        /// <summary>
        /// Shutdown the observatory operations. Triggered by the <see cref="Shutting"/> event.
        /// </summary>
        /// <returns>Whether observatory has been shut down successfully.</returns>
        /// <remarks>
        /// <para>
        /// The shutdown procedure is as follows:
        /// <list type="number">
        ///     <item><see cref="Interrupt"/> any scheduled shutdown (since it is being realised).</item>
        ///     <item>Connect to the <see cref="Mount"/> and <see cref="Dome"/>.</item>
        ///     <item><see cref="StopWeatherProtection"/>.</item>
        ///     <item><see cref="heliomaster.CommonTimelapse.Stop"/> the timelapses.</item>
        ///     <item><see cref="UnSlaveDomeFromMount"/>.</item>
        ///     <item>
        ///         Simultaneously:
        ///         <list type="bullet">
        ///             <item><see cref="BaseCamera.Stop"/> the connected cameras.</item>
        ///             <item><see cref="heliomaster.Telescope.Park"/> the <see cref="Mount"/>.</item>
        ///             <item>Close the <see cref="Dome"/> shutter via <see cref="heliomaster.Dome.SmartShutter"/>.</item>
        ///         </list>
        ///     </item>
        ///     <item>Park the <see cref="Dome"/>.</item>
        ///     <item>Transition to <see cref="AutomationStates.Idle"/> and raise <see cref="ShutdownSuccess"/>.</item>
        /// </list>
        /// If any exception occurs during this procedure or if the <see cref="Dome"/> cannot be closed or the
        /// <see cref="Mount"/> cannot be parked, raise <see cref="ShutdownFailure"/>, transition to
        /// <see cref="AutomationStates.Faulted"/>, and raise <see cref="Fixing"/>. Return <c>false</c> in this case,
        /// otherwise <c>true</c>.
        /// </para>
        /// 
        /// <para>All exceptions thrown in this method are caught and <see cref="Emit"/>-ted, instead of propagated up
        /// the call stack.</para>
        /// </remarks>
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
                if (e is ObservatoryException exception) Emit(exception);
                ShutdownFailureRaise(e);
                AutomationState = AutomationStates.Faulted;
                FixingRaise();
                return false;
            } finally {
                autosem.Release();
            }
        }

        /// <summary>
        /// Attempt to fix the state of the observatory, so that the hardware is safe. This comprises mainly parking
        /// the <see cref="Mount"/> and closing the <see cref="Dome"/>. 
        /// </summary>
        /// <returns>Whether the observatory state has been fixed successfully.</returns>
        ///
        /// <remarks>
        /// <list type="number">
        ///     <item>Transition to <see cref="AutomationStates.Fixing"/>.</item>
        ///     <item>
        ///         Independently (but one after another):
        ///         <list type="number">
        ///             <item>
        ///                 Fix the <see cref="Dome"/> state:
        ///                 <list type="number">
        ///                     <item>Connect to the <see cref="Dome"/>.</item>
        ///                     <item>
        ///                         Close the <see cref="Dome"/> via <see cref="heliomaster.Dome.SmartShutter"/>.
        ///                         <list type="bullet">
        ///                             <item>If this fails, throw a <see cref="FixingFailedError"/>.</item>
        ///                         </list>
        ///                     </item>
        ///                     <item>
        ///                         Park the <see cref="Dome"/> via <see cref="heliomaster.Dome.HomeOrPark"/>.
        ///                         <list type="bullet">
        ///                             <item>If this fails, throw a <see cref="AutoOperationsWarning"/>.</item>
        ///                         </list>
        ///                     </item>
        ///                 </list>
        ///             </item>
        ///             <item>
        ///                 Fix the <see cref="Mount"/> state:
        ///                 <list type="number">
        ///                     <item>Connect to the <see cref="Mount"/>.</item>
        ///                     <item>
        ///                         <see cref="heliomaster.Telescope.Park"/> the <see cref="Mount"/>.
        ///                         <list type="bullet">
        ///                             <item>If this fails, throw a <see cref="FixingFailedError"/>.</item>
        ///                         </list>
        ///                     </item>
        ///                 </list>
        ///             </item>
        ///         </list>
        ///         Any exceptions thrown in either of the two sub-procedures is caught and <see cref="Emit"/>-ted it as an
        ///         <see cref="AutoOperationsException"/>.
        ///     </item>
        ///     <item>If any of the two sub-procedures has failed, <see cref="Emit"/> a
        ///         <see cref="CriticalObservatoryError"/>, which should notify the operator, raise
        ///         <see cref="FixingFailure"/>, transition to <see cref="AutomationStates.Faulted"/>, and return
        ///         <c>false</c>. Otherwise, transition to <see cref="AutomationStates.Idle"/>, raise
        ///         <see cref="FixingSuccess"/>, and return <c>true</c>.</item>
        /// </list>
        ///
        /// <para>All exceptions thrown in this method are caught and <see cref="Emit"/>-ted, instead of propagated up
        /// the call stack.</para>
        /// </remarks>
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
