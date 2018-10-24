using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;
using heliomaster.Annotations;
using heliomaster.Properties;

namespace heliomaster {
    public class Weather : BaseHardwareControl {
        protected override Type driverType => typeof(ObservingConditions);
        public virtual IObservingConditions Driver => driver as IObservingConditions;

        public override string Type => Resources.weather;

        #region ITEMS
        
        ///<summary> See <see cref="IObservingConditions.CloudCover"/></summary>
        public WeatherItem CloudCover { get; private set; }

        ///<summary> See <see cref="IObservingConditions.DewPoint"/></summary>
        public WeatherItem DewPoint { get; private set; }

        ///<summary> See <see cref="IObservingConditions.Humidity"/></summary>
        public WeatherItem Humidity { get; private set; }

        ///<summary> See <see cref="IObservingConditions.Pressure"/></summary>
        public WeatherItem Pressure { get; private set; }

        ///<summary> See <see cref="IObservingConditions.RainRate"/></summary>
        public WeatherItem RainRate { get; private set; }

        ///<summary> See <see cref="IObservingConditions.SkyBrightness"/></summary>
        public WeatherItem SkyBrightness { get; private set; }

        ///<summary> See <see cref="IObservingConditions.SkyQuality"/></summary>
        public WeatherItem SkyQuality { get; private set; }

        ///<summary> See <see cref="IObservingConditions.SkyTemperature"/></summary>
        public WeatherItem SkyTemperature { get; private set; }

        ///<summary> See <see cref="IObservingConditions.StarFWHM"/></summary>
        public WeatherItem StarFWHM { get; private set; }

        ///<summary> See <see cref="IObservingConditions.Temperature"/></summary>
        public WeatherItem Temperature { get; private set; }

        ///<summary> See <see cref="IObservingConditions.WindDirection"/></summary>
        public WeatherItem WindDirection { get; private set; }

        ///<summary> See <see cref="IObservingConditions.WindGust"/></summary>
        public WeatherItem WindGust { get; private set; }

        ///<summary> See <see cref="IObservingConditions.WindSpeed"/></summary>
        public WeatherItem WindSpeed { get; private set; }
        
        /// <summary> Used to synchronize access to <see cref="Items"/>. </summary>
        private readonly ReaderWriterLockSlim itemslock = new ReaderWriterLockSlim();
        
        /// <summary> The available weather items. See <see cref="Initialize"/>. </summary>
        public ObservableCollection<WeatherItem> Items { get; } = new ObservableCollection<WeatherItem>();
        
        #endregion

        /// <summary> Whether the weather condition is regarded as safe (not bad). </summary>
        /// <value> <c>false</c> if <see cref="Condition"/> is <see cref="WeatherItem.Conditions.Bad"/>, <c>null</c> if
        /// it is <c>null</c> and <c>true</c> otherwise. </value>
        public bool? Safe => Condition == null ? (bool?) null : Condition != WeatherItem.Conditions.Bad;

        /// <summary> The current weather condition. Calculated as the "worst" of all the weather items. </summary>
        /// <value> The maximal (worst) value from the conditions of the weather items, or <c>null</c> if no
        /// properties are available or an error occurs.</value>
        public WeatherItem.Conditions? Condition {
            get {
                itemslock.EnterReadLock();
                try {
                    var ret = (Valid && Items.Count > 0) ? Items.Max(i => i.Condition) : (WeatherItem.Conditions?) null;
                    return ret;
                } catch {
                    return null;
                } finally {
                    itemslock.ExitReadLock();
                }
            }
        }

        protected readonly string[] propNames = {
            "CloudCover", "DewPoint", "Humidity", "Pressure", "RainRate", "SkyBrightness", "SkyQuality",
            "SkyTemperature", "StarFWHM", "Temperature", "WindDirection", "WindGust", "WindSpeed"
        };
        protected static readonly string[] baseProps = { nameof(Safe), nameof(Condition) };
        protected readonly List<string> properties = new List<string>(baseProps);
        protected override IEnumerable<string> props => properties;
        
        /// <summary> Initialize a weather controller by populating its properties with available weather items. </summary>
        /// <remarks> Iterates over all ASCOM <see cref="IObservingConditions"/> properties, as listed in
        /// <see cref="propNames"/>, and checks whether the corresponding property has been implemented in the driver.
        /// If it is (<see cref="WeatherItem.Valid"/> returns <c>true</c>), then the item (it's name) is also added to
        /// <see cref="properties"/>, which lists all available weather items.
        /// </remarks>
        protected override void initialize() {
            itemslock.EnterWriteLock();
            try {
                foreach (var p in propNames)
                    if (typeof(Weather).GetProperty(p) is PropertyInfo pinfo) {
                        var witem = new WeatherItem(Driver, p);
                        pinfo.SetValue(this, witem);
                        OnPropertyChanged(p);

                        if (witem.Valid) {
                            Items.Add(witem);
                            properties.Add(p);
                        }
                    }
            }
            finally {
                itemslock.ExitWriteLock();
            }
        }

        /// <summary> Requests a refresh of the driver and notifies that the weather items are updated. </summary>
        protected override void refresh() {
            if (Valid) Driver.Refresh();
            itemslock.EnterReadLock();
            try { foreach (var i in Items) try { i.NotifyChanged(); } catch { } }
            finally { itemslock.ExitReadLock(); }
        }

        /// <summary> Clears the weather items and delegates to the base method
        /// <see cref="BaseHardwareControl.Disconnect"/>. </summary>
        public override Task Disconnect() {
            Items.Clear();
            properties.Clear(); properties.AddRange(baseProps); // Return the array to the initial state.
            return base.Disconnect();
        }

        /// <summary> Populate a <see cref="WeatherSettings"/> instance with the current weather items. </summary>
        /// <param name="s">The settings instance to populate.</param>
        public void SaveInSettings(WeatherSettings s) {
            foreach (var name in propNames)
                if (typeof(Weather).GetProperty(name) is PropertyInfo pinfo &&
                    pinfo.GetValue(this) is WeatherItem witem)
                    s[name] = witem;
        }
    }
}
