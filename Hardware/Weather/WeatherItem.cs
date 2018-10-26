using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Windows.Media;
using ASCOM.DeviceInterface;
using heliomaster.Properties;

namespace heliomaster {
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class WeatherItem : BaseNotify {
        /// <summary> Represents the three weather conditions. </summary>
        public enum Conditions {
            OK, Warning, Bad
        }
        
        // TODO: better way to associate conditions to colors? Maybe a converter?
        public static readonly Dictionary<Conditions, Color> ConditionColors = new Dictionary<Conditions, Color> {
            {Conditions.OK,      S.Settings.colorOK},
            {Conditions.Warning, S.Settings.colorWarning},
            {Conditions.Bad,     S.Settings.colorBad}
        };


        /// <summary> The minimum sensible value of the quantity measured. E.g. for percentage values this is 0. </summary>
        public double Min { get; set; } = double.NaN;
        /// <summary> The maximum sensible value of the quantity measured. E.g. for percentage values this is 100. </summary>
        public double Max { get; set; } = double.NaN;

        /// <summary> Whether higher values should be interpreted as safer, e.g. SkyQuality. </summary>
        public bool IsReversed;

        /// <summary> The string representation of the item's units. </summary>
        public string Unit { get; set; }
        /// <summary> The format string used to display the value of the item. Useful for controlling decimal digits. </summary>
        public string ValueFormat { get; set; } = "{0:F0}";

        /// <summary> The key in the application resource dictionary of the icon to associate with this WeatherItem. </summary>
        public string IconKey { get; set; }


        private readonly IObservingConditions driver; // the driver to extract values from
        private readonly PropertyInfo         pinfo;  // used to extract the item value from the driver


        /// <summary> Raise <see cref="BaseNotify.OnPropertyChanged"/> for each of the mutable properties of the WeatherItem. </summary>
        public void NotifyChanged() {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(Condition));
        }

        /// <summary> Whether the driver supports this item. This is checked only once in the constructor. </summary>
        public bool Valid { get; }
        /// <summary> The reading for the item or <c>null</c> if not available. </summary>
        public double? Value => Valid ? (double?) pinfo.GetValue(driver) : null;

        /// <summary> The name of the <see cref="IObservingConditions"/> property associated to this item. </summary>
        public string Name { get; }

        private string _displayName;
        /// <summary> The name of this item for display purposes. Defaults to the value of <see cref="Name"/>. </summary>
        public string DisplayName { get => _displayName ?? Name; set => _displayName = value; }

        /// <summary> The string representing the item. Combines the (formatted) value and the unit. </summary>
        public string DisplayValue => $"{string.Format(ValueFormat, Value)} {Unit}";
        
        /// <summary> Whether an icon is associated with this item. </summary>
        public bool   HasIcon      => IconKey != null;

        private double _boundLow = double.NegativeInfinity;
        /// <summary> Low bound of the warning interval. See <see cref="Condition"/>. </summary>
        public double BoundLow {
            get => _boundLow;
            set {
                var toval = value < Min ? Min : value;
                if (toval.Equals(_boundLow)) return;
                _boundLow = toval;
                OnPropertyChanged();
            }
        }

        private double _boundHigh = double.PositiveInfinity;
        /// <summary> High bound of the warning interval. See <see cref="Condition"/>. </summary>
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
        /// <summary> Whether higher values should be interpreted as safer, e.g. SkyQuality. See <see cref="Condition"/>. </summary>
        public bool BoundsReversed {
            get => _boundsReversed;
            set {
                if (value == _boundsReversed) return;
                _boundsReversed = value;
                OnPropertyChanged();
            }
        }

        private bool _neglect;
        /// <summary> Whether to disregard this item, when determining the overall weather condition. </summary>
        public bool Neglect {
            get => _neglect;
            set {
                if (value == _neglect) return;
                _neglect = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSetRange));
            }
        }

        public bool CanSetRange => !Neglect; // indicates whether bound controls should be displayed

        /// <summary> The deemed condition associated with this WeatherItem. </summary>
        /// <remarks> The two values <see cref="BoundLow"/> and <see cref="BoundHigh"/> determine the range in which
        /// values will lead to the warning state. If <see cref="BoundsReversed"/> is <c>true</c>, values higher than
        /// <see cref="BoundHigh"/> will be considered <see cref="WeatherItem.Conditions.OK"/>, and those lower than
        /// <see cref="BoundLow"/> will lead to the <see cref="WeatherItem.Conditions.Bad"/> state, and vice versa if
        /// the bounds are reversed. In both cases however, values exactly on the boundaries will lead to the "better"
        /// state being reported, i.e. <see cref="WeatherItem.Conditions.Warning"/> instead of
        /// <see cref="WeatherItem.Conditions.Bad"/> and <see cref="WeatherItem.Conditions.OK"/> instead of
        /// <see cref="WeatherItem.Conditions.Warning"/>.
        /// </remarks>
        public Conditions Condition =>
            Neglect ? Conditions.OK :
                BoundsReversed
                    ? ((Value >= BoundHigh) ? Conditions.OK : (Value >= BoundLow) ? Conditions.Warning : Conditions.Bad)
                    : ((Value <= BoundLow)  ? Conditions.OK : (Value <= BoundHigh) ? Conditions.Warning : Conditions.Bad);

        // ReSharper disable once UnusedMember.Global
        // Used by deserializer!
        public WeatherItem() {}
        
        /// <summary>
        /// Create a new WeatherItem, associated to the property named <paramref name="name"/> of the given driver
        /// <paramref name="d"/>.
        /// </summary>
        /// <remarks> The constructor extracts the saved values for the unit, icon, bounds, etc. from the saved instance
        /// of the corresponding WeatherItem in <see cref="S.Weather"/>.
        /// </remarks>
        /// <param name="d"></param>
        /// <param name="name"></param>
        /// <param name="displayName"></param>
        public WeatherItem(IObservingConditions d, string name, string displayName = null) {
            driver      = d;
            Name        = name;
            DisplayName = displayName;

            var wi = (WeatherItem) S.Weather[name];
            Unit           = wi.Unit;
            ValueFormat    = wi.ValueFormat;
            IconKey        = wi.IconKey;
            Neglect        = wi.Neglect;
            Min            = wi.Min;
            Max            = wi.Max;
            BoundLow       = wi.BoundLow;
            BoundHigh      = wi.BoundHigh;
            BoundsReversed = wi.IsReversed;

            // Check if the property is implemented.
            // If not, accessing it will raise an error, and Valid will remain false.
            if (typeof(IObservingConditions).GetProperty(name) is PropertyInfo _pinfo) {
                try {
                    _pinfo.GetValue(driver);
                    pinfo = _pinfo;
                    Valid = true;
                } catch { }
            }
        }
    }
}
