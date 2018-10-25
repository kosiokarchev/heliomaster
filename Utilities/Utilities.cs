using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ASCOM.Utilities;
using heliomaster.Annotations;

namespace heliomaster {
    /// <summary>
    /// Base for classes that need to implement <see cref="INotifyPropertyChanged"/>. Allows calling
    /// <see cref="BaseNotify.OnPropertyChanged"/> without an argument to automatically raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for the property from which the call is made.
    /// </summary>
    public abstract class BaseNotify : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Provides utility functions.
    /// </summary>
    public static partial class Utilities {
        /// <summary>
        /// An instance of <see cref="Util"/> to be reused throughout the code.
        /// </summary>
        public static readonly Util ASCOMUtil = new Util();

        
        /// <summary>
        /// Perform an action (usually a web request) with HTTPS server validation bypassed.
        /// </summary>
        /// <remarks>Used when communicating with a Netio socket which only pretends to support HTTPS fully.</remarks>
        /// <param name="a">Action to perform.</param>
        public static Task InsecureSSL(Action a) {
            return Task.Run(() => {
                var back = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                a();
                ServicePointManager.ServerCertificateValidationCallback = back;
            });
        }
        
        
        /// <summary>
        /// Determine the image file format that corresponds to a filename based on its extension.
        /// </summary>
        /// <param name="fname">The filename to inspect.</param>
        /// <returns>A <see cref="CameraImage.ImageFileFormat"/> or <c>null</c> if one cannot be determined.</returns>
        public static CameraImage.ImageFileFormat? FormatFromExtension(string fname) {
            var ext = Path.GetExtension(fname)?.ToLower();
            switch (ext) {
                case ".png":
                    return CameraImage.ImageFileFormat.png;
                case ".jpeg": case ".jpg":
                    return CameraImage.ImageFileFormat.jpeg;
                case ".tiff": case ".tif":
                    return CameraImage.ImageFileFormat.tiff;
                default:
                    return null;
            }
        }
    }
    
    // Mathematical utilities
    public static partial class Utilities {
        /// <summary>
        /// Return <paramref name="a"/> modulo <paramref name="b"/> in the range <c>[-|b|/2; |b|/2]</c>.
        /// </summary>
        /// <remarks>Normally <c>|a%b|</c> is in the range <c>[0; |b|)</c>, and the sign is that of <c>a</c>. The result
        /// of this function is in the range <c>(-|b|/2; |b|/2]</c> for positive <c>a</c> and <c>[-|b|/2; |b|/2)</c>
        /// otherwise.</remarks>
        /// <param name="a">The dividend.</param>
        /// <param name="b">The divisor.</param>
        public static double SymModulo(double a, double b) {
            var m  = a % b;
            var hb = b / 2;
            return (m > hb) ? m - hb
                : (m < -hb) ? m + hb
                : m;
        }

        /// <summary>
        /// Return <paramref name="a"/> modulo <paramref name="b"/> in the range <c>[0; |b|)</c>.
        /// </summary>
        /// <remarks>Normally <c>|a%b|</c> is in the range <c>[0; |b|)</c>, and the sign is that of <c>a</c>, so this
        /// function ensures even negative dividends return positive moduli.</remarks>
        /// <param name="a">The dividend.</param>
        /// <param name="b">The divisor.</param>
        public static double PositiveModulo(double a, double b) {
            var m = a % b;
            return m >= 0 ? m : m + Math.Abs(b);
        }

        /// <summary>
        /// Convert degrees to radians.
        /// </summary>
        /// <param name="x">An angle in degrees.</param>
        public static double deg2rad(double x) => (Math.PI / 180) * x;
        
        /// <summary>
        /// Convert radians to degrees.
        /// </summary>
        /// <param name="x">An angle in radians.</param>
        public static double rad2dec(double x) => (180 / Math.PI) * x;

        /// <summary>
        /// Return a positive number, useful for setting lower limits in logarithmic scales.
        /// </summary>
        /// <param name="x">A suggestion.</param>
        /// <returns><paramref name="x"/> if it is positive, otherwise a small constant
        /// <see cref="Properties.Settings.rateNonZero"/></returns>
        public static double NonZero(double x) {
            return x > 0 ? x : Properties.Settings.Default.rateNonZero;
        }

        /// <summary>
        /// Return a number in the range [<paramref name="min"/>; <paramref name="max"/>].
        /// </summary>
        /// <remarks>The result is undefined if <paramref name="min"/> is bigger than <paramref name="max"/>.</remarks>
        /// <param name="x">A suggestion.</param>
        /// <param name="min">The lower limit of the interval.</param>
        /// <param name="max">The upper limit of the interval.</param>
        /// <returns><paramref name="x"/> if it is in the range, and otherwise the closer limit.</returns>
        public static double Clamp(double x, double min, double max) {
            return Math.Min(max, Math.Max(min, x));
        }

        /// <summary>
        /// Remap the range [0;1] to [<paramref name="min"/>; <paramref name="max"/>] exponentially.
        /// </summary>
        /// <param name="x">The value to convert.</param>
        /// <param name="min">The output for x=0.</param>
        /// <param name="max">The output for x=1.</param>
        /// <returns>The value whose logarithm divides (log(min); log(max)) in the same ratio x divides (0; 1).</returns>
        /// <example>
        /// <code>ScaleLogToLin(0.5, 2, 8) => 4;</code>
        /// </example>
        /// <seealso cref="ScaleLogToLin"/>
        public static double ScaleLinToLog(double x, double min, double max) {
            return min * Math.Pow(max / min, x);
        }

        /// <summary>
        /// Remap the range [<paramref name="min"/>; <paramref name="max"/>] to [0;1] logarithmically.
        /// </summary>
        /// <param name="Y">The value to convert.</param>
        /// <param name="min">The value that would output 0.</param>
        /// <param name="max">The value that would output 1.</param>
        /// <returns>The value which divides (0; 1) in the same ratio as log(Y) divides (log(min); log(max)).</returns>
        /// <example>
        /// <code>ScaleLogToLin(4, 2, 8) => 0.5;</code>
        /// </example>
        /// <seealso cref="ScaleLinToLog"/>
        public static double ScaleLogToLin(double Y, double min, double max) {
            return Math.Log(Y / min) / Math.Log(max / min);
        }
    }
    
    
    // Formatting utilities.
    public static partial class Utilities {
        /// <summary>
        /// Format an angle in degrees, choosing a unit so that the displayed value is bigger than one, unless it is
        /// smaller than 1mas, in which case it is formatted as mas.
        /// </summary>
        /// <remarks>If <paramref name="r"/> is bigger than 1°, it is formatted as degrees; if it is smaller than
        /// that but bigger than 1arcmin, it is formatted as such; otherwise, as arcsec if it is bigger than 1arcsec
        /// and as milli-arc seconds if it is smaller than 1mas.</remarks>
        /// <param name="r">The angle to format</param>
        /// <param name="fmt">A format to apply to the number portion once it is converted to the correct units.</param>
        /// <example>
        /// <code>
        /// AngleFormatter(2.145, "{0:F1}") => "2.1 °";
        /// AngleFormatter(0.214, "{0:F1}") => "12.8 '";
        /// AngleFormatter(0.002, "{0:F1}") => "7.2 \"";
        /// AngleFormatter(2.145e-4, "{0:F1}") => "772.2 mas";
        /// </code>
        /// </example>
        public static string AngleFormatter(double r, string fmt) {
            double num;
            string unit;
            if (r >= 1) {                   num = r;           unit = "°";
            } else if (r >= 1.0 / 60.0) {   num = 60 * r;      unit = "'";
            } else if (r >= 1.0 / 3600.0) { num = 3600 * r;    unit = "\"";
            } else {                        num = 3600000 * r; unit = "mas"; }

            return string.Format(fmt, num) + " " + unit;
        }

        /// <summary>
        /// A function that formats a rate in degrees per second given as its only argument using <see cref="AngleFormatter"/>. 
        /// </summary>
        public static Func<double, string> RateFormatter => r => AngleFormatter(r, "{0:0.#}") + "/s";

        /// <summary>
        /// Format an exception as "[exception type]: [message]".
        /// </summary>
        /// <param name="e">An exception to format.</param>
        public static string FormatException(Exception e) {
            return $"{e.GetType().Name}: {e.Message}";
        }
    }
}
