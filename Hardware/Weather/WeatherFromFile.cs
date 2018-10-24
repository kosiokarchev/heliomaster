using System;
using System.Threading.Tasks;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;

namespace heliomaster {
    /// <summary>
    /// A "hardware" controller reading weather data from a file. It integrates the <see cref="FileOC"/> driver
    /// framework into the <see cref="Weather"/> pattern.
    /// </summary>
    /// <remarks>
    /// Several member overrides are necessary for the correct functioning of this class. These are all the members
    /// which access <see cref="AscomDriver.Connected"/>, <see cref="AscomDriver.Name"/>, and
    /// <see cref="AscomDriver.Dispose"/>, namely <see cref="BaseHardwareControl.valid"/>,
    /// <see cref="BaseHardwareControl.Name"/>, <see cref="BaseHardwareControl.connect"/>, and
    /// <see cref="BaseHardwareControl.disconnect"/>. They are replaced by identical (in code) copies, which however in
    /// the context of WeatherFromFile access the corresponding new members in <see cref="FileOC"/>. This is a hack
    /// necessary because these members are non-inheritable in AscomDriver, and neither is it an interface implementation...
    /// </remarks>
    public class WeatherFromFile : Weather {
        public override IObservingConditions Driver => driver as FileOC;

        /// <summary> Create a new instance of a WeatherFromFile, given a <see cref="FileOC"/> subtype. </summary>
        /// <param name="type">The type of the requested driver. The default is <see cref="BoltwoodFileOC"/>.</param>
        /// <exception cref="ArgumentException">If <paramref name="type"/> is not a subclass of <see cref="FileOC"/>.</exception>
        public WeatherFromFile(Type type = null) {
            if (type == null)
                type = typeof(BoltwoodFileOC);
            else if (!type.IsSubclassOf(typeof(FileOC)))
                throw new ArgumentException($"\"{nameof(type)}\" parameter must be a subclass of {nameof(FileOC)}");

            driverType = type;
        }

        /// <summary> Identical override of <see cref="P:heliomaster.BaseHardwareControl.valid" />. </summary>
        protected override bool valid => Driver?.Connected == true;
        /// <summary> Identical override of <see cref="P:heliomaster.BaseHardwareControl.Name" />. </summary>
        public override string Name => Valid ? Driver?.Name : null;

        /// <summary> Identical override of <see cref="M:heliomaster.BaseHardwareControl.connect(System.Boolean,System.Boolean)" />. </summary>
        protected override async Task<bool> connect(bool init = true, bool setup=false) {
            var ret = await Task.Run(() => {
                try {
                    Driver.Connected = true;
                    return Driver.Connected;
                } catch (ASCOM.DriverException) {
                    return false;
                }
            });

            if (init && ret)
                Initialize();

            return ret;
        }

        /// <summary> Identical override of <see cref="M:heliomaster.BaseHardwareControl.disconnect" />. </summary>
        protected override Task disconnect() {
            return Task.Run(() => {
                if (Valid) {
                    Driver.Connected = false;
                    Driver.Dispose();
                    driver = null;
                }
            });
        }
    }
}
