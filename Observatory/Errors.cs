using System;

namespace heliomaster {
    public abstract class ObservatoryException : Exception {
        public ObservatoryException() { }
        public ObservatoryException(string message) : base(message) { }
    }

    public abstract class ObservatoryWarning : ObservatoryException {
        public ObservatoryWarning() { }
        public ObservatoryWarning(string message) : base(message) { }
    }

    public class SlavingWarning : ObservatoryWarning {
        public SlavingWarning() { }
        public SlavingWarning(string message) : base(message) { }
    }

    public class AutoOperationsWarning : ObservatoryWarning {
        public AutoOperationsWarning() { }
        public AutoOperationsWarning(string message) : base(message) { }
    }
    public class RefuseAutomationWarning : AutoOperationsWarning {
        public RefuseAutomationWarning() { }
        public RefuseAutomationWarning(string message) : base(message) { }
    }

    public class AutoOperationsException : ObservatoryException {
        public AutoOperationsException() { }
        public AutoOperationsException(string message) : base(message) { }
    }

    public class HardwareError : AutoOperationsException {
        public HardwareError() { }
        public HardwareError(string message) : base(message) { }
    }
    public class ConnectionError : HardwareError {
        public ConnectionError() { }
        public ConnectionError(string message) : base(message) { }
    }
    public class FixingFailedError : AutoOperationsException {
        public FixingFailedError() { }
        public FixingFailedError(string message) : base(message) { }
    }

    public class ObjectNotLocatedError : AutoOperationsException {
        public ObjectNotLocatedError() { }
        public ObjectNotLocatedError(string message) : base(message) { }
    }

    public class CriticalObservatoryError : ObservatoryException {
        public CriticalObservatoryError() { }
        public CriticalObservatoryError(string message) : base(message) { }
    }
}
