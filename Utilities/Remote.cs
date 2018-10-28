using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;

namespace heliomaster {
    /// <summary>
    /// Interface allowing the upload of a file or file stream.
    /// </summary>
    public interface IUploader {
        /// <summary>
        /// Upload a file.
        /// </summary>
        /// <param name="fname">The name of the file to upload.</param>
        /// <param name="path">The path on the remote server to upload to.</param>
        /// <returns>A <see cref="Task"/> which completes when the upload is complete (or fails).</returns>
        Task Upload(string fname, string path);
        /// <summary>
        /// Upload a file from a stream.
        /// </summary>
        /// <param name="istream">The file stream to upload.</param>
        /// <param name="path">The path on the remote server to upload to.</param>
        /// <returns>A <see cref="Task"/> which completes when the upload is complete (or fails).</returns>
        Task Upload(Stream istream, string path);
    }

    public enum RemoteLoginMethods {
        [Description("Password")] UserPass,
        [Description("Private key file")] PrivateKey
    }

    /// <summary>
    /// Represents an SSH command.
    /// </summary>
    public class Command {
        /// <summary>
        /// The command as a string.
        /// </summary>
        public string text;
        /// <summary>
        /// The command as a <see cref="SshCommand"/>.
        /// </summary>
        /// <remarks>Created in the <see cref="Execute"/> method.</remarks>
        public SshCommand cmd;
        
        /// <summary>
        /// The output stream from running the command or <c>null</c> if it has not yet been executed.
        /// </summary>
        /// <seealso cref="SshCommand.Result"/>
        public string stdout => cmd?.Result;
        /// <summary>
        /// The error stream from running the command or <c>null</c> if it has not yet been executed.
        /// </summary>
        /// <seealso cref="SshCommand.Error"/>
        public string stderr => cmd?.Error;
        /// <summary>
        /// The exit code of the command or <c>null</c> if it has not yet been executed.
        /// </summary>
        /// <seealso cref="SshCommand.ExitStatus"/>
        public int? ExitCode => cmd?.ExitStatus;
        /// <summary>
        /// Whether the command completed successfully, i.e. has already returned an <see cref="ExitCode"/> of 0.
        /// </summary>
        public bool Success => ExitCode == 0;

        /// <summary>
        /// Create a new command from its string representation.
        /// </summary>
        public Command(string command) { text = command; }

        /// <summary>
        /// Raised when the command has finished executing.
        /// </summary>
        public event EventHandler Completed;
        private void CompletedRaise() {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Execute the command using the provided <see cref="SshClient"/>.
        /// </summary>
        /// <param name="ssh">The client to use for executing the command.</param>
        /// <returns>A <see cref="Task"/> which completes when the command  and all callbacks
        /// from <see cref="Completed"/> finish executing.</returns>
        public Task Execute(SshClient ssh) {
            cmd?.Dispose();
            cmd = ssh.CreateCommand(text);
            return Task.Factory.FromAsync((callback, state) => cmd.BeginExecute(callback, state), res => {
                cmd.EndExecute(res);
                CompletedRaise();
            }, null);
        }
    }

    /// <summary>
    /// Represents a remote connection used to execute SSH commands using <see cref="SshClient"/> and transfer files
    /// using <see cref="SftpClient"/>.
    /// </summary>
    public class Remote : IUploader {
        /// <summary>
        /// The login details for the connection. Created by the <see cref="Init(string, string, object, int, bool)"/>
        /// method and overrides.
        /// </summary>
        public ConnectionInfo ConnectionInfo;

        /// <summary>
        /// The last error while executing an SSH command (if any).
        /// </summary>
        public Exception SSHError;
        private SshClient _ssh;
        /// <summary>
        /// Gets the SSH client or creates a new one and attempts to connect it before returning it.
        /// </summary>
        /// <remarks>If an error occurs during the connection process, it is saved into <see cref="SSHError"/>.</remarks>
        /// <exception cref="NullReferenceException">If <see cref="ConnectionInfo"/> has not been set.</exception>
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

        /// <summary>
        /// The last error while executing an SFTP operation (if any).
        /// </summary>
        public Exception SFTPError;
        private SftpClient _sftp;
        /// <summary>
        /// Gets the SFTP client or creates a new one and attempts to connect it before returning it.
        /// </summary>
        /// <remarks>If an error occurs during the connection process, it is saved into <see cref="SFTPError"/>.</remarks>
        /// <exception cref="NullReferenceException">If <see cref="ConnectionInfo"/> has not been set.</exception>
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

        /// <summary>
        /// Whether both SSH and SFTP services are connected.
        /// </summary>
        public bool Connected => SSH.IsConnected && SFTP.IsConnected;

        /// <summary>
        /// Closes any existing connections and, if <see cref="connect"/>, attempts to (re)connect.
        /// </summary>
        /// <remarks>After this function returns, it is possible that only one of <see cref="SSH"/> and
        /// <see cref="SFTP"/> is connected.</remarks>
        /// <param name="connect">Whether to attempt to connect.</param>
        /// <returns>Whether both services are now </returns>
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

        /// <summary>
        /// Initialise the connection parameters (<see cref="ConnectionInfo"/>) and optionally connect the services.
        /// </summary>
        /// <param name="host">The host name to connect to.</param>
        /// <param name="user">The user name to use.</param>
        /// <param name="keyfile">The <see cref="PrivateKeyFile"/> to use for authentication.</param>
        /// <param name="port">The port to connect to on the remote host.</param>
        /// <param name="connect">Whether to attempt to connect the services.</param>
        /// <returns>The result of <see cref="Reset"/>(<paramref name="connect"/>)</returns>
        public bool Init(string host, string user, PrivateKeyFile keyfile, int port, bool connect=true) {
            ConnectionInfo = new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, keyfile));
            return Reset(connect);
        }
        /// <summary>
        /// Initialise the connection parameters (<see cref="ConnectionInfo"/>) and optionally connect the services.
        /// </summary>
        /// <param name="host">The host name to connect to.</param>
        /// <param name="user">The user name to use.</param>
        /// <param name="pass">The password to use for authentication.</param>
        /// <param name="port">The port to connect to on the remote host.</param>
        /// <param name="connect">Whether to attempt to connect the services.</param>
        /// <returns>The result of <see cref="Reset"/>(<paramref name="connect"/>)</returns>
        public bool Init(string host, string user, string pass, int port, bool connect=true) {
            ConnectionInfo = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, pass));
            return Reset(connect);
        }

        /// <summary>
        /// Execute a command given as a string.
        /// </summary>
        /// <returns>A <see cref="Task"/> which returns the <see cref="Command"/> once it has been executed.</returns>
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
