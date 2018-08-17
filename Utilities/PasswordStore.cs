using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace heliomaster {
    public static class PassowordStoreUtil {
        private static readonly byte[] entropy = Encoding.Unicode.GetBytes("Непротивоконституциоснователствувайте!");

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


    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class PasswordStore : BaseNotify {
        private SecureString _securePass = new SecureString();
        [XmlIgnore] public SecureString SecurePass {
            get => _securePass;
            set {
                if (Equals(value, _securePass)) return;
                _securePass = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StringPass));
                OnPropertyChanged(nameof(EncryptedPass));
            }
        }

        [XmlIgnore]
        public string StringPass {
            get => SecurePass.ToInsecureString();
            set => SecurePass = value.ToSecureString();
        }

        public string EncryptedPass {
            get => SecurePass.EncryptString();
            set => SecurePass = value.DecryptString();
        }
    }
}
