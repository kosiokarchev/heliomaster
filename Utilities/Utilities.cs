using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using heliomaster_wpf.Annotations;

namespace heliomaster_wpf {
    public static class SecureIt {
        private static readonly byte[] entropy = Encoding.Unicode.GetBytes("Salt Is Not A Password");

        public static string EncryptString(this SecureString input) {
            if (input == null) return null;
            return Convert.ToBase64String(
                ProtectedData.Protect(Encoding.Unicode.GetBytes(input.ToInsecureString()), entropy,
                                      DataProtectionScope.CurrentUser));
        }

        public static SecureString DecryptString(this string encryptedData) {
            if (encryptedData == null) return null;

            try {
                return Encoding.Unicode.GetString(
                    ProtectedData.Unprotect(
                        Convert.FromBase64String(encryptedData), entropy, DataProtectionScope.CurrentUser
                    )
                ).ToSecureString();
            } catch {
                return new SecureString();
            }
        }

        public static SecureString ToSecureString(this IEnumerable<char> input) {
            if (input == null) return null;

            var secure = new SecureString();

            foreach (var c in input)
                secure.AppendChar(c);

            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(this SecureString input) {
            if (input == null) return null;

            var ptr = Marshal.SecureStringToBSTR(input);

            try { return Marshal.PtrToStringBSTR(ptr); }
            finally { Marshal.ZeroFreeBSTR(ptr); }
        }
    }

    public abstract class BaseNotify : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class Utilities {
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

        public static double SymModulo(double a, double b) {
            var m  = a % b;
            var hb = b / 2;
            return (m > hb) ? m - hb
                : (m < -hb) ? m + hb
                : m;
        }

        public static double PositiveModulo(double a, double b) {
            var m = a % b;
            return m >= 0 ? m : m + b;
        }

        public static double deg2rad(double x) => (Math.PI / 180) * x;
        public static double rad2dec(double x) => (180 / Math.PI) * x;

        public static double NonZero(double x) {
            return x > 0 ? x : Properties.Settings.Default.rateNonZero;
        }

        public static double Clamp(double x, double min, double max) {
            return Math.Min(max, Math.Max(min, x));
        }

        public static double ScaleLinToLog(double x, double min, double max) {
            return min * Math.Pow(max / min, x);
        }

        public static double ScaleLogToLin(double Y, double min, double max) {
            return Math.Log(Y / min) / Math.Log(max / min);
        }

        public static string AngleFormatter(double r, string fmt) {
            double num;
            string unit;
            if (r >= 1) {                   num = r;           unit = "°";
            } else if (r >= 1.0 / 60.0) {   num = 60 * r;      unit = "'";
            } else if (r >= 1.0 / 3600.0) { num = 3600 * r;    unit = "\"";
            } else {                        num = 3600000 * r; unit = "mas"; }

            return string.Format(fmt, num) + " " + unit;
        }

        public static Func<double, string> RateFormatter => r => AngleFormatter(r, "{0:0.#}") + "/s";


        public static readonly Util ASCOMUtil = new Util();
        public static readonly Dictionary<ShutterState, string> ShutterStateStrings = new Dictionary<ShutterState, string> {
            {ShutterState.shutterOpen, "open"},
            {ShutterState.shutterClosed, "closed"},
            {ShutterState.shutterOpening, "opening"},
            {ShutterState.shutterClosing, "closing"},
            {ShutterState.shutterError, "error"}
        };

        public static Task InsecureSSL(Action a) {
            return Task.Run(() => {
                var back = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                a();
                ServicePointManager.ServerCertificateValidationCallback = back;
            });
        }
    }
}
