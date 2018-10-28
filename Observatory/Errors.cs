using System;

namespace heliomaster {
    /// <summary>
    /// A base class for exceptions thrown during observatory operations. 
    /// </summary>
    public abstract class ObservatoryException : Exception {
        public ObservatoryException() { }
        public ObservatoryException(string message) : base(message) { }
    }

    /// <summary>
    /// A base class for warnings emitted during observatory operations.
    /// </summary>
    public abstract class ObservatoryWarning : ObservatoryException {
        public ObservatoryWarning() { }
        public ObservatoryWarning(string message) : base(message) { }
    }

    /// <summary>
    /// A warning originating from software syncing of the dome.
    /// </summary>
    public class SlavingWarning : ObservatoryWarning {
        public SlavingWarning() { }
        public SlavingWarning(string message) : base(message) { }
    }

    /// <summary>
    /// A warning related to inability to perform certain operation steps automatically.
    /// </summary>
    public class AutoOperationsWarning : ObservatoryWarning {
        public AutoOperationsWarning() { }
        public AutoOperationsWarning(string message) : base(message) { }
    }
    /// <summary>
    /// An <see cref="AutoOperationsWarning"/> of the decision to not proceed with automated operations due to e.g. bad weather.
    /// </summary>
    public class RefuseAutomationWarning : AutoOperationsWarning {
        public RefuseAutomationWarning() { }
        public RefuseAutomationWarning(string message) : base(message) { }
    }

    /// <summary>
    /// An error that has occurred during automatic operations. 
    /// </summary>
    public class AutoOperationsException : ObservatoryException {
        public AutoOperationsException() { }
        public AutoOperationsException(string message) : base(message) { }
    }

    /// <summary>
    /// An <see cref="AutoOperationsException"/> related to some hardware malfunction or unexpected state.
    /// </summary>
    public class HardwareError : AutoOperationsException {
        public HardwareError() { }
        public HardwareError(string message) : base(message) { }
    }
    /// <summary>
    /// A <see cref="HardwareError"/> related to connection issues with the hardware.
    /// </summary>
    public class ConnectionError : HardwareError {
        public ConnectionError() { }
        public ConnectionError(string message) : base(message) { }
    }
    /// <summary>
    /// An <see cref="AutoOperationsException"/> occurring because fixing the state of the observatory is impossible.
    /// </summary>
    public class FixingFailedError : AutoOperationsException {
        public FixingFailedError() { }
        public FixingFailedError(string message) : base(message) { }
    }

    /// <summary>
    /// An <see cref="AutoOperationsException"/> occurring because automatic object location has failed.
    /// </summary>
    public class ObjectNotLocatedError : AutoOperationsException {
        public ObjectNotLocatedError() { }
        public ObjectNotLocatedError(string message) : base(message) { }
    }

    /// <summary>
    /// An error that definitely requires human intervention.
    /// </summary>
    public class CriticalObservatoryError : ObservatoryException {
        public CriticalObservatoryError() { }
        public CriticalObservatoryError(string message) : base(message) { }
    }
}
