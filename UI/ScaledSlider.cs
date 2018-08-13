using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ASCOM.Utilities;
using heliomaster_wpf.Annotations;

namespace heliomaster_wpf {
    public class CustomToolTipSlider : Slider, INotifyPropertyChanged {
        public static readonly DependencyProperty ToolTipFormatProperty = DependencyProperty.Register(nameof(ToolTipFormat), typeof(string), typeof(CustomToolTipSlider), new PropertyMetadata(defaultValue:"{0}"));
        public string ToolTipFormat {
            get => GetValue(ToolTipFormatProperty) as string;
            set => SetValue(ToolTipFormatProperty, value);
        }

        public static readonly DependencyProperty ToolTipFormatterProperty = DependencyProperty.Register(nameof(ToolTipFormatter), typeof(Func<double, object>), typeof(CustomToolTipSlider));
        public Func<double, object> ToolTipFormatter {
            get => GetValue(ToolTipFormatterProperty) as Func<double, object>;
            set => SetValue(ToolTipFormatterProperty, value);
        }

        private ToolTip _autoToolTip;
        private ToolTip AutoToolTip => _autoToolTip ??
                                       (_autoToolTip = typeof(Slider).GetField(
                                           "_autoToolTip",
                                           BindingFlags.NonPublic | BindingFlags.Instance
                                       )?.GetValue(this) as ToolTip);

        protected virtual double ToolTipValue => Value;
        public object ToolTipContent => ReadLocalValue(ToolTipFormatterProperty) != DependencyProperty.UnsetValue
            ? ToolTipFormatter(ToolTipValue)
            : String.Format(ToolTipFormat, ToolTipValue);
        protected void ModifyToolTip() {
            if (AutoToolTip != null)
                AutoToolTip.Content = ToolTipContent;
            OnPropertyChanged(nameof(ToolTipContent));
        }

        protected override void OnThumbDragStarted(DragStartedEventArgs e) {
            base.OnThumbDragStarted(e);
            ModifyToolTip();
        }

        protected override void OnThumbDragDelta(DragDeltaEventArgs e) {
            base.OnThumbDragDelta(e);
            ModifyToolTip();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ScaledSlider : CustomToolTipSlider {
        static ScaledSlider() {
            ValueProperty.OverrideMetadata(
                typeof(ScaledSlider),
                new FrameworkPropertyMetadata(propertyChangedCallback:(d, e) => {
                    if (e.OldValue.Equals(e.NewValue)) return;
                    (d as ScaledSlider)?.SetValue(
                        CustomValueProperty,
                        ((ScaledSlider) d).ValueToCustom((double) e.NewValue));
                }));
        }

        public static void SetCustomValue(ScaledSlider d, double val) {
            d.Value = Utilities.Clamp(d.CustomToValue(val), d.Minimum, d.Maximum);
            d.ModifyToolTip();
        }

        public static readonly DependencyProperty CustomMinimumProperty = DependencyProperty.Register(
            nameof(CustomMinimum), typeof(double), typeof(ScaledSlider),
            new PropertyMetadata(propertyChangedCallback: (d, e) => {
                SetCustomValue((ScaledSlider) d, ((ScaledSlider) d).CustomValue);
            }));
        public double CustomMinimum {
            get => GetValue(CustomMinimumProperty) is double ? (double) GetValue(CustomMinimumProperty) : 0;
            set => SetValue(CustomMinimumProperty, value);
        }
        public static readonly DependencyProperty CustomMaximumProperty = DependencyProperty.Register(
            nameof(CustomMaximum), typeof(double), typeof(ScaledSlider),
            new PropertyMetadata(propertyChangedCallback: (d, e) => {
                SetCustomValue((ScaledSlider) d, ((ScaledSlider) d).CustomValue);
            }));
        public double CustomMaximum {
            get => GetValue(CustomMaximumProperty) is double ? (double) GetValue(CustomMaximumProperty) : 0;
            set => SetValue(CustomMaximumProperty, value);
        }

        private static double placebo(double x) => x;
        public Func<double, double> LogValueToCustom => x => Utilities.ScaleLinToLog(x, Utilities.NonZero(CustomMinimum), Utilities.NonZero(CustomMaximum));
        public Func<double, double> LogCustomToValue => x => Utilities.ScaleLogToLin(x, Utilities.NonZero(CustomMinimum), Utilities.NonZero(CustomMaximum));

        public static readonly DependencyProperty CustomValueProperty = DependencyProperty.Register(
            nameof(CustomValue), typeof(double), typeof(ScaledSlider),
            new PropertyMetadata(propertyChangedCallback: (d, e) => {
                if (!e.OldValue.Equals(e.NewValue)) return;
                SetCustomValue((ScaledSlider) d, (double) e.NewValue);
            }));
        public static readonly DependencyProperty ValueToCustomProperty = DependencyProperty.Register(nameof(ValueToCustom), typeof(Func<double, double>), typeof(ScaledSlider), new PropertyMetadata(defaultValue:(Func<double, double>) placebo));
        public static readonly DependencyProperty CustomToValueProperty = DependencyProperty.Register(nameof(CustomToValue), typeof(Func<double, double>), typeof(ScaledSlider), new PropertyMetadata(defaultValue:(Func<double, double>) placebo));


        public Func<double, double> ValueToCustom { get => GetValue(ValueToCustomProperty) as Func<double, double>; set => SetValue(ValueToCustomProperty, value); }
        public Func<double, double> CustomToValue { get => GetValue(CustomToValueProperty) as Func<double, double>; set => SetValue(CustomToValueProperty, value); }

        public double CustomValue { get => (double) GetValue(CustomValueProperty); set => SetValue(CustomValueProperty, value); }
        protected override double ToolTipValue => CustomValue;
    }
}
