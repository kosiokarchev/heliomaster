using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xaml;
using ASCOM.DeviceInterface;

namespace heliomaster {
    /// <summary>
    /// A base class for converters, etc. that allows their simple use in XAML circumventing the need to define them as
    /// static resources.
    /// </summary>
    public class BaseMarkupExtension : MarkupExtension {
        public override object ProvideValue(IServiceProvider serviceProvider) {
            return this;
        }
    }

    public class ChainConverter : List<IValueConverter>, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return this.Aggregate(value, (current, converter) => converter.Convert(current, targetType, parameter, culture));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class VisibilityConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value != null && (bool) value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class WeatherSafeConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return DependencyProperty.UnsetValue;

            var color = (bool) value
                ? WeatherItem.ConditionColors[WeatherItem.Conditions.OK]
                : WeatherItem.ConditionColors[WeatherItem.Conditions.Bad];

            return targetType == typeof(Brush) ? (object) new SolidColorBrush(color) : color;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class WeatherConditionConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return DependencyProperty.UnsetValue;

            var color = WeatherItem.ConditionColors[(WeatherItem.Conditions) value];
            var ret = targetType == typeof(Brush) ? (object) new SolidColorBrush(color) : color;
            return ret;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class RadioToIntConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value != null && value.Equals(int.Parse((string) parameter));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value != null && (bool) value)
                return int.Parse((string) parameter);
            else
                return Binding.DoNothing;
        }
    }

    public class IndexConverter : BaseMarkupExtension, IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            try {
                var ret = ((dynamic) values[0])[(int) values[1]];
                return ret;
            } catch (Exception) {
                return Binding.DoNothing;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            return new []{Binding.DoNothing, Binding.DoNothing};
        }
    }

    public class EqualityConverter : BaseMarkupExtension, IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values[0].Equals(values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            return new []{Binding.DoNothing, Binding.DoNothing};
        }
    }

    public class AngleDisplayConverter : BaseMarkupExtension,  IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null || double.IsNaN((double) value)) return Binding.DoNothing;
            switch ((string) parameter) {
                case "hours":   return Utilities.ASCOMUtil.HoursToHMS((double) value);
                case "degrees": return Utilities.ASCOMUtil.DegreesToDMS((double) value);
                default:        return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value;
        }
    }


    public class ShutterStateToIconConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is ShutterState s
                && Application.Current.TryFindResource(
                    s == ShutterState.shutterOpen    ? "icon-dome-open" :
                    s == ShutterState.shutterClosed  ? "icon-dome-closed" :
                    s == ShutterState.shutterOpening ? "icon-dome-opening" :
                    s == ShutterState.shutterClosing ? "icon-dome-closing" : "icon-dome-error"
                ) is Canvas c)
                return new Viewbox { Child = c };
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }


    public class StaticResourceConverter : MarkupExtension, IValueConverter {
        private Control _target;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string resourceKey)
                return _target?.FindResource(resourceKey) ?? Application.Current.FindResource(resourceKey);
            else return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            var rootObjectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            if (rootObjectProvider == null)
                return this;

            _target = rootObjectProvider.RootObject as Control;
            return this;
        }
    }


    public class EnumToIntConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value != null && targetType == typeof(int)) ? (int) value : Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value is int val) ? Enum.ToObject(targetType, val) : Binding.DoNothing;
        }
    }


    public class EnumBindingSourceExtension : MarkupExtension {
        public Type EnumType { get; }

        public EnumBindingSourceExtension(Type enumType) {
            enumType = Nullable.GetUnderlyingType(enumType) ?? enumType;
            if (!enumType.IsEnum)
                throw new ArgumentException("Type must be an Enum.");
            EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            var ret = new List<string>();
            foreach (var val in Enum.GetValues(EnumType)) {
                var descrattr = (DescriptionAttribute[])
                    EnumType.GetField(val.ToString())
                            .GetCustomAttributes(typeof(DescriptionAttribute), false);
                ret.Add(descrattr.Length > 0 ? descrattr[0].Description : val.ToString());
            }
            return ret;
        }
    }

    public class RateFormatterConverter : BaseMarkupExtension, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value != null) return Utilities.RateFormatter((double) value);
            else return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
