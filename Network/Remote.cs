using System;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Renci.SshNet;

namespace heliomaster {
    public interface IUploader {
        Task Upload(string fname, string path);
        Task Upload(Stream istream, string path);
    }

    public enum RemoteLoginMethods {
        [Description("Password")] UserPass,
        [Description("Private key file")] PrivateKey
    }

    public class Command {
        public string     text;
        public SshCommand cmd;
        public string     stdout   => cmd.Result;
        public string     stderr   => cmd.Error;
        public int?       ExitCode => cmd?.ExitStatus;
        public bool       Success  => ExitCode == 0;

        public Command(string command) { text = command; }

        private void OnCompleted() {
            Completed?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler Completed;

        public Task Execute(SshClient ssh) {
            cmd?.Dispose();
            cmd = ssh.CreateCommand(text);
            return Task.Factory.FromAsync((callback, state) => cmd.BeginExecute(callback, state), res => {
                cmd.EndExecute(res);
                OnCompleted();
            }, null);
        }
    }

    public class Remote : IUploader {
        public ConnectionInfo ConnectionInfo;

        public Exception SSHError;
        private SshClient _ssh;
        public SshClient SSH {
            get {
                if (ConnectionInfo == null)
                    throw new NullReferenceException("Connection info is not set yet.");
                if (_ssh == null)
                    _ssh = new SshClient(ConnectionInfo);
                if (!_ssh.IsConnected)
                    try { _ssh.Connect(); }
                    catch (Exception e) { SSHError = e; throw; }
                return _ssh;
            }
        }

        public Exception SFTPError;
        private SftpClient _sftp;
        public SftpClient SFTP {
            get {
                if (ConnectionInfo == null)
                    throw new NullReferenceException("Connection info is not set yet.");
                if (_sftp == null)
                    _sftp = new SftpClient(ConnectionInfo);
                if (!_sftp.IsConnected)
                    try { _sftp.Connect(); }
                    catch (Exception e) { SFTPError = e; throw; }
                return _sftp;
            }
        }

        public bool Connected => SSH.IsConnected && SFTP.IsConnected;

        private bool Reset(bool connect = true) {
            _ssh?.Dispose();  _ssh = null;  SSHError = null;
            _sftp?.Dispose(); _sftp = null; SFTPError = null;

            if (connect) {
                bool cssh = false, csftp = false;
                try { cssh = SSH.IsConnected; } catch {}
                try { csftp = SFTP.IsConnected; } catch {}

                return cssh && csftp;
            } else return false;
        }

        public bool Init(string host, string user, object auth, int port, bool connect) {
            AuthenticationMethod authmethod;
            if (auth is PrivateKeyFile f) {
                authmethod = new PrivateKeyAuthenticationMethod(user, f);
            } else if (auth is string s) {
                authmethod = new PasswordAuthenticationMethod(user, s);
            } else {
                throw new ArgumentException("auth must be string or PrivateKeyFile");
            }
            ConnectionInfo = new ConnectionInfo(host, port, user, authmethod);
            return Reset();
        }

        public bool Init(string host, string user, PrivateKeyFile keyfile, int port, bool connect=true) {
            ConnectionInfo = new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, keyfile));
            return Reset(connect);
        }
        public bool Init(string host, string user, string pass, int port, bool connect=true) {
            ConnectionInfo = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, pass));
            return Reset(connect);
        }

        public Task<Command> Execute(string command) {
            return Task<Command>.Factory.StartNew(() => {
                if (command != null) {
                    var ret = new Command(command);
                    Logger.debug("REMOTE: " + command);
                    ret.Execute(SSH).Wait();
                    Logger.debug($"REMOTE: {command} --> {ret.ExitCode}");
                    return ret;
                } else return null;
            });
        }

        public Task Upload(string fname, string path) {
            return Upload(File.OpenRead(fname), path);
        }

        public Task Upload(Stream istream, string path) {
            return Task.Factory.FromAsync((callback, state) => SFTP.BeginUploadFile(istream, path, callback, state), SFTP.EndUploadFile, null);
        }
    }
}
