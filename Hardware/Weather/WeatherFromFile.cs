using System;
using System.Threading.Tasks;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;

namespace heliomaster {
    public class WeatherFromFile : Weather {
        protected override Type driverType { get; }
        public override IObservingConditions Driver => driver as FileOC;

        public WeatherFromFile(Type type = null) {
            if (type == null)
                type = typeof(BoltwoodFileOC);
            else if (!type.IsSubclassOf(typeof(FileOC)))
                throw new ArgumentException($"\"type\" parameter must be a subclass of {nameof(FileOC)}");

            driverType = type;
        }

        protected override bool valid => Driver?.Connected == true;
        public override string Name => Valid ? Driver?.Name : null;

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
