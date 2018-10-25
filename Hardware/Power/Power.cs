using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace heliomaster {
    public class PowerStatus {
        public bool? On;
    }

    /// <summary>
    /// A base class for power controllers.
    /// </summary>
    public abstract class BasePower : BaseNotify {
        /// <summary>
        /// A collection of strings that identify the different sockets. Typically used when communicating with the
        /// controller (in the backend).
        /// </summary>
        [XmlIgnore] public abstract ObservableCollection<string> Names { get; protected set; }
        
        /// <summary>
        /// Whether controlling power using this controller is currently possible.
        /// </summary>
        [XmlIgnore] public abstract bool Available { get; }
        
        /// <summary>
        /// Register a device with the power controller under the given name.
        /// </summary>
        /// <param name="o">An object to be associated with the registration. This is the front-facing identifier
        /// for users of the power controller.</param>
        /// <param name="name">A name to be associated with the registration. This is usually used when communicating
        /// with the controller (backend identifier).</param>
        /// <returns>Whether the registration was successful. It is up to the subclasses to define a successful
        /// registration.</returns>
        public abstract bool Register(object o, string name);

        /// <summary>
        /// Turn the device identified by the given object on.
        /// </summary>
        /// <param name="o">The registered identifier for the device.</param>
        /// <returns>The current power status of the device after the operation.</returns>
        public abstract Task<PowerStatus> On(object o);
        
        /// <summary>
        /// Turn the device identified by the given object off.
        /// </summary>
        /// <param name="o">The registered identifier for the device.</param>
        /// <returns>The current power status of the device after the operation.</returns>
        public abstract Task<PowerStatus> Off(object o);
        
        /// <summary>
        /// Toggle the power of the device identified by the given object.
        /// </summary>
        /// <remarks>Some power controllers have a specific instruction for toggling.</remarks>
        /// <param name="o">The registered identifier for the device.</param>
        /// <returns>The current power status of the device after the operation.</returns>
        public abstract Task<PowerStatus> Toggle(object o);

        /// <summary>
        /// Turn the device identified by the given object off and then back on.
        /// </summary>
        /// <remarks>Some power controllers have a specific instruction for resetting.</remarks>
        /// <param name="o">The registered identifier for the device.</param>
        /// <param name="dt">The time to spend in the off state.</param>
        /// <returns>The current power status of the device after the operation.</returns>
        public abstract Task<PowerStatus> Reset(object o, TimeSpan? dt = null);
        
        /// <summary>
        /// Turn the device identified by the given object on and then back off.
        /// </summary>
        /// <remarks>Some power controllers have a specific instruction for pulsing.</remarks>
        /// <param name="o">The registered identifier for the device.</param>
        /// <param name="dt">The time to spend in the on state (duration of the pulse).</param>
        /// <returns>The current power status of the device after the operation.</returns>
        public abstract Task<PowerStatus> Pulse(object o, TimeSpan? dt = null);
    }

    /// <summary>
    /// An enumeration containing the implemented power controller types.
    /// </summary>
    /// <remarks>When a new <see cref="BasePower"/> subclass is implemented, it should be added here.</remarks>
    public enum PowerTypes {
        /// <summary>
        /// A configuration that does not allow programmatic power control.
        /// </summary>
        Basic,
        
        /// <summary>
        /// A Netio4 power controller implemented by the <see cref="heliomaster.Netio.Power"/> class.
        /// </summary>
        Netio
    }
}
