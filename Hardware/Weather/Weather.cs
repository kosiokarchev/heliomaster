using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;
using heliomaster.Annotations;
using heliomaster.Properties;

namespace heliomaster {
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class WeatherItem : BaseNotify {
        public enum Conditions {
            OK, Warning, Bad
        }
        public static readonly Dictionary<Conditions, Color> ConditionColors = new Dictionary<Conditions, Color> {
            {Conditions.OK,      S.Settings.colorOK},
            {Conditions.Warning, S.Settings.colorWarning},
            {Conditions.Bad,     S.Settings.colorBad}
        };


        public double Min { get; set; } = Double.NaN;
        public double Max { get; set; } = Double.NaN;
        public bool   IsReversed;
        public string Unit { get; set; }
        public string ValueFormat { get; set; } = "{0:F0}";
        public string IconKey { get; set; }


        private readonly IObservingConditions driver;
        private readonly PropertyInfo pinfo;


        public void NotifyChanged() {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(Condition));
        }

        public bool    Valid { get; }
        public double? Value => Valid ? (double?) pinfo.GetValue(driver) : null;

        public string Name         { get; }
        public string DisplayName  { get; }
        public string DisplayValue => $"{String.Format(ValueFormat, Value)} {Unit}";
        public bool   HasIcon      => IconKey != null;

        private double _boundLow = Double.NegativeInfinity;
        public double BoundLow {
            get => _boundLow;
            set {
                var toval = value < Min ? Min : value;
                if (toval.Equals(_boundLow)) return;
                _boundLow = toval;
                OnPropertyChanged();
            }
        }

        private double _boundHigh = Double.PositiveInfinity;
        public double BoundHigh {
            get => _boundHigh;
            set {
                var toval = value > Max ? Max : value;
                if (toval.Equals(_boundHigh)) return;
                _boundHigh = toval;
                OnPropertyChanged();
            }
        }

        private bool _boundsReversed;
        public bool BoundsReversed {
            get => _boundsReversed;
            set {
                if (value == _boundsReversed) return;
                _boundsReversed = value;
                OnPropertyChanged();
            }
        }

        private bool _neglect;
        public bool Neglect {
            get => _neglect;
            set {
                if (value == _neglect) return;
                _neglect = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSetRange));
            }
        }

        public bool CanSetRange => !Neglect;

        public Conditions Condition =>
            Neglect ? Conditions.OK :
            BoundsReversed
                ? ((Value >= BoundHigh) ? Conditions.OK : (Value >= BoundLow) ? Conditions.Warning : Conditions.Bad)
                : ((Value <= BoundLow) ? Conditions.OK : (Value <= BoundHigh) ? Conditions.Warning : Conditions.Bad);

        // ReSharper disable once UnusedMember.Global
        // Used by deserializer!
        public WeatherItem() {}
        public WeatherItem(IObservingConditions _driver, string name, string displayName = null) {
            driver      = _driver;
            Name        = name;
            DisplayName = displayName ?? name;

            var p = (WeatherItem) WeatherSettings.Default[name];
            Unit           = p.Unit;
            ValueFormat    = p.ValueFormat;
            IconKey        = p.IconKey;
            BoundsReversed = p.IsReversed;
            Neglect        = p.Neglect;
            Min            = p.Min;
            Max            = p.Max;
            BoundLow       = p.BoundLow;
            BoundHigh      = p.BoundHigh;


            try {
                if (typeof(IObservingConditions).GetProperty(name) is PropertyInfo _pinfo) {
                    _pinfo.GetValue(driver);
                    pinfo = _pinfo;
                    Valid = true;
                }
            } catch (Exception) {
                Valid = false;
            }
        }
    }


    public class Weather : BaseHardwareControl {
        protected override Type driverType => typeof(ObservingConditions);
        public virtual IObservingConditions Driver => driver as ObservingConditions;

        public override string Type => Resources.weather;

        public WeatherItem CloudCover     { get; private set; }
        public WeatherItem DewPoint       { get; private set; }
        public WeatherItem Humidity       { get; private set; }
        public WeatherItem Pressure       { get; private set; }
        public WeatherItem RainRate       { get; private set; }
        public WeatherItem SkyBrightness  { get; private set; }
        public WeatherItem SkyQuality     { get; private set; }
        public WeatherItem SkyTemperature { get; private set; }
        public WeatherItem StarFWHM       { get; private set; }
        public WeatherItem Temperature    { get; private set; }
        public WeatherItem WindDirection  { get; private set; }
        public WeatherItem WindGust       { get; private set; }
        public WeatherItem WindSpeed      { get; private set; }

        public bool? Safe => Condition == null ? (bool?) null : Condition != WeatherItem.Conditions.Bad;

        public WeatherItem.Conditions? Condition {
            get {
                itemslock.EnterReadLock();
                var ret = (Valid && Items.Count > 0) ? Items.Max(i => i.Condition) : (WeatherItem.Conditions?) null;
                itemslock.ExitReadLock();
                return ret;
            }
        }

        public ObservableCollection<WeatherItem> Items { get; } = new ObservableCollection<WeatherItem>();

        protected readonly string[] propNames = {
            "CloudCover", "DewPoint", "Humidity", "Pressure", "RainRate", "SkyBrightness", "SkyQuality",
            "SkyTemperature", "StarFWHM", "Temperature", "WindDirection", "WindGust", "WindSpeed"
        };

        protected readonly List<string> properties = new List<string> {nameof(Safe), nameof(Condition)};
        protected override IEnumerable<string> props => properties;

        private readonly ReaderWriterLockSlim itemslock = new ReaderWriterLockSlim();
        protected override void RefreshHandle() {
            if (Valid) Driver.Refresh();
            base.RefreshHandle();
            itemslock.EnterReadLock();
            foreach (var i in Items)
                i.NotifyChanged();
            itemslock.ExitReadLock();
        }

        public override void Initialize() {
            itemslock.EnterWriteLock();
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
            itemslock.ExitWriteLock();

            base.Initialize();
        }

        public void SaveInSettings(WeatherSettings s) {
            foreach (var name in propNames)
                if (typeof(Weather).GetProperty(name) is PropertyInfo pinfo &&
                    pinfo.GetValue(this) is WeatherItem witem)
                    s[name] = witem;
        }
    }
}
