using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace heliomaster {
    /// <summary>
    /// A utility class containing extension methods for converting between <see cref="String"/> and
    /// <see cref="SecureString"/> and for encrypting and decrypting the latter. 
    /// </summary>
    public static class PassowordStoreUtil {
        /// <summary>
        /// Longest Bulgarian word: Don't act against the constitution!
        /// </summary>
        private static readonly byte[] entropy = Encoding.Unicode.GetBytes("Непротивоконституционствувателствувайте!");

        /// <summary>
        /// Return an encrypted version of the <see cref="SecureString"/>.
        /// </summary>
        public static string EncryptString(this SecureString input) {
            if (input == null) return null;
            return Convert.ToBase64String(
                ProtectedData.Protect(Encoding.Unicode.GetBytes(input.ToInsecureString()), entropy,
                                      DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Create a <see cref="SecureString"/> from its plain-string encrypted representation (decrypt it).
        /// </summary>
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

        /// <summary>
        /// Put the characters of the <paramref name="input"/> string into a <see cref="SecureString"/>.
        /// </summary>
        public static SecureString ToSecureString(this IEnumerable<char> input) {
            if (input == null) return null;

            var secure = new SecureString();

            foreach (var c in input)
                secure.AppendChar(c);

            secure.MakeReadOnly();
            return secure;
        }

        /// <summary>
        /// Extract the characters from the <see cref="SecureString"/> <paramref name="input"/> into a plain string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
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
