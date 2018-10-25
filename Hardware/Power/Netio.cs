using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using heliomaster.Annotations;
using Newtonsoft.Json;

namespace heliomaster.Netio {
    /// <summary> States in the Netio Json API. </summary>
    public enum States {
        Off = 0,
        On  = 1
    }
    
    /// <summary> Actions in the Netio Json API. </summary>
    public enum OutputActions {
        TurnOff  = 0,
        TurnOn   = 1,
        OffDelay = 2,
        OnDelay  = 3,
        Toggle   = 4,
        None     = 5,
        Ignore   = 6,
    }

    /// <summary> A member of the Netio Json API status response. Not used in the application. </summary>
    [UsedImplicitly] public class Agent {
        public string   Model,    Version, JSONVer, DeviceName, MAC;
        public int      VendorID, OemID,   Uptime,  NumOutputs;
        public DateTime Time;
    }
    
    /// <summary> A member of the Netio Json API status response. Not used in the application. </summary>
    [UsedImplicitly] public class GlobalMeasure {
        public double   Voltage, Frequency, TotalCurrent, OverallPowerFactor, TotalLoad, TotalEnergy;
        public DateTime EnergyStart;
    }
    
    /// <summary> A member of the Netio Json API representing the state of a single socket. </summary>
    /// <remarks>This class contains two methods <see cref="ShouldSerializeDelay"/> and
    /// <see cref="ShouldSerializeState"/> which are used by <see cref="JsonConvert.SerializeObject(object)"/>
    /// in order to include the <see cref="Delay"/> only if the <see cref="Action"/> requires it and the
    /// <see cref="State"/> only if the <see cref="Action"/> is <see cref="OutputActions.Ignore"/> as per the Netio
    /// Json API requirements.</remarks>
    public class Output {
        public int           ID;
        public string        Name { internal get; set; }
        public States        State;
        public OutputActions Action;
        public int           Delay;

        public bool ShouldSerializeDelay() => Delay >= 100 && (Action == OutputActions.OffDelay || Action == OutputActions.OffDelay);
        public bool ShouldSerializeState() => Action == OutputActions.Ignore;
//        [JsonIgnore] public double        Current, PowerFactor, Load, Energy;
    }
    
    /// <summary>
    /// Represents the response retrieved from the Netio Json API. Its only useful member is <see cref="Outputs"/> which
    /// contains the actual data as <see cref="Output"/> objects.
    /// </summary>
    public class Netio {
        [JsonIgnore] public Agent                Agent;
        [JsonIgnore] public GlobalMeasure        GlobalMeasure;
        public              IEnumerable<Output>  Outputs;
    }

    /// <summary>
    /// A Netio4 power controller controlled using <a href="https://www.netio-products.com/files/download/sw/version/JSON---popis-NETIO-M2M-API-rozhrani_1-0-0.pdf">the Json API</a>.
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class Power : BasePower {
        private readonly UriBuilder uriBuilder = new UriBuilder {Path = "netio.json"};

        /// <summary>
        /// The hostname or IP of the controller.
        /// </summary>
        public string Host {
            get => uriBuilder.Host;
            set {
                if (value == uriBuilder.Host) return;
                uriBuilder.Host = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// The port the controller is configured to operate on.
        /// </summary>
        /// <remarks>Defaults to 80 for <see cref="UseHttps"/><c>==false</c> and 443 for HTTPS.</remarks>
        public int Port {
            get => uriBuilder.Port == -1 ? (UseHttps ? 443 : 80) : uriBuilder.Port;
            set {
                if (value == uriBuilder.Port) return;
                uriBuilder.Port = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// Whether to use an HTTPS connection.
        /// </summary>
        /// <remarks>Changing this value will lead to an automatic reset of the <see cref="Port"/> to the corresponding
        /// default.</remarks>
        public bool UseHttps {
            get => uriBuilder.Scheme == Uri.UriSchemeHttps;
            set {
                var scheme = value ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
                if (scheme.Equals(uriBuilder.Scheme)) return;
                uriBuilder.Scheme = scheme;
                uriBuilder.Port = -1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Port));
            }
        }
        
        [XmlIgnore] public int UseHttpsIndex {
            get => UseHttps ? 1 : 0;
            set => UseHttps = value == 1;
        }

        /// <summary>
        /// The username to use for the Netio Json API.
        /// </summary>
        private string _user;
        public string User {
            get => _user;
            set {
                if (value == uriBuilder.UserName) return;
                _user = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The password to use for the Netio Json API as a secure string.
        /// </summary>
        [XmlIgnore] public SecureString Pass { get; set; } = new SecureString();
        
        /// <summary>
        /// The password to use for the Netio Json API as a plain string.
        /// </summary>
        /// <remarks>This is not kept in memory by the class but is extracted from <see cref="Pass"/> every time this
        /// property is accessed. Similarly, setting this in fact sets <see cref="Pass"/>.</remarks>
        [XmlIgnore] public string PassString {
            get => Pass.ToInsecureString();
            set => Pass = value.ToSecureString();
        }

        /// <summary>
        /// An encrypted string representing the password used for saving in settings files.
        /// </summary>
        [UsedImplicitly] public string EncryptedPassword {
            get => Pass.EncryptString();
            set => Pass = value.DecryptString();
        }

        /// <summary>
        /// The timeout for requests to the Netio Json API. Defaults to 10s.
        /// </summary>
        public TimeSpan Timeout {
            get => hc.Timeout;
            set {
                if (value.Equals(hc.Timeout)) return;
                try {
                    hc.Timeout = value;
                } catch {
                    hc.Timeout = new TimeSpan(0, 0, 10);
                }
                OnPropertyChanged();
            }
        }

        [XmlIgnore] private bool faulted;
        [XmlIgnore] private Netio _socket;
        /// <summary>
        /// Holds the last successfully retrieved state of the controller.
        /// </summary>
        /// <value>Returns <c>null</c> if the last attempt failed. If the state has not yet been retrieved, getting
        /// this property automatically calls <see cref="Get"/> and returns the result. Setting this property also
        /// resets the <see cref="Names"/> collection.</value>
        [XmlIgnore] public Netio Socket {
            get => _socket ?? (faulted ? null : _socket = Get().Result);
            set {
                _socket = value;
                faulted = value == null;
                Names = new ObservableCollection<string>();
            }
        }

        private Uri uri => uriBuilder.Uri;
        private readonly HttpClient hc = new HttpClient();

        /// <summary>
        /// Construct a GET or POST request to send to the Netio Json API.
        /// </summary>
        /// <param name="get">Whether to send a GET or POST request.</param>
        /// <returns>A GET or POST request. In the latter case, the <see cref="HttpRequestMessage.Content"/> property
        /// must be set separately.</returns>
        private HttpRequestMessage constructMessage(bool get=true) {
            return new HttpRequestMessage {
                Method     = get ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = uri,
                Headers = {
                    Authorization =
                        new AuthenticationHeaderValue(
                            "Basic",
                            Convert.ToBase64String(
                                Encoding.ASCII.GetBytes($"{User}:{new NetworkCredential(User, Pass).Password}")))
                }
            };
        }

        /// <summary>
        /// Construct a POST request to send to the Netio Json API.
        /// </summary>
        /// <param name="os">The desired state of the outputs.</param>
        /// <returns>A POST request setting the Netio outputs to the desired states. </returns>
        private HttpRequestMessage constructMessage(IEnumerable<Output> os) {
            var ret = constructMessage(false);
            var json = JsonConvert.SerializeObject(new Netio {Outputs = os});
            ret.Content = new StringContent(json);
            Logger.debug($"NETIO: Sending: {json}");
            return ret;
        }


        /// <summary>
        /// A Semaphore which prevents multiple simultaneous requests to the controller.
        /// </summary>
        private readonly SemaphoreSlim sem = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// Send the specified message to the controller and return the response.
        /// </summary>
        /// <remarks>
        /// <para>This function sends the specified message and waits for a response. The Netio API should
        /// return an identical response for any request that it has handled successfully, namely the complete current
        /// state of the controller. Receiving a parsable <see cref="Netio"/> object therefore represents success,
        /// so the function saves the state in the <see cref="Socket"/> property and returns it. If any error occurs,
        /// either in the connection or the parsing code, or the controller is unable to handle the request, the
        /// <see cref="Socket"/> is set to <c>null</c>, meaning that <see cref="faulted"/> is now <c>true</c>. In this
        /// case the function returns <c>null</c>, which is the value of <see cref="Socket"/>.</para>
        /// <para>This is the only function which directly communicates with the controller. It keeps its "state" by
        /// using <see cref="sem"/>. If it is called again from a different thread before a previous instance has
        /// completed, it will wait for one second for the semaphore to be released and if not, will quit and return the
        /// current value of <see cref="Socket"/> without modification and without making a request.
        /// </para></remarks>
        /// <param name="msg">The message to send, as constructed by <see cref="constructMessage(bool)"/>. This could either
        /// be a GET or a POST.</param>
        /// <returns>The state of the controller returned by the API.</returns>
        private Netio sendMessage(HttpRequestMessage msg) {
            Exception           exc      = null;
            HttpResponseMessage response = null;

            var cancel = new CancellationTokenSource();
            var semwait = sem.WaitAsync(cancel.Token);
            if (Task.WaitAny(new[] {semwait}, TimeSpan.FromSeconds(1)) != 0)
                cancel.Cancel();
            else {
                try {
                    Utilities.InsecureSSL(() => {
                        try {
                            var task = hc.SendAsync(msg, HttpCompletionOption.ResponseContentRead,
                                                    new CancellationTokenSource(Timeout).Token);
                            task.Wait();
                            if (task.Status == TaskStatus.RanToCompletion)
                                response = task.Result;
                        } catch (Exception err) {
                            exc = err;
                        }
                    }).Wait();

                    if (exc != null)
                        throw exc;

                    if (response.IsSuccessStatusCode) {
                        var json = response?.Content.ReadAsStringAsync().Result;
                        Socket = JsonConvert.DeserializeObject<Netio>(json);
                    } else {
                        Logger.debug($"NETIO: Got bad response: {response.StatusCode}");
                        Socket = null;
                    }
                } catch (Exception e) {
                    Logger.debug($"NETIO: Error in {msg.Method}: {Utilities.FormatException(e)}");
                    Socket = null;
                } finally {
                    sem.Release();
                }
            }

            return _socket;
        }

        /// <summary>
        /// Perform a GET request to the Netio Json API and return the controller state. 
        /// </summary>
        /// <remarks>This function simply calls <see cref="sendMessage"/> with the result of
        /// <see cref="constructMessage(bool)"/>.</remarks>
        /// <returns>The return value of <see cref="sendMessage"/>, identical to <see cref="Socket"/></returns>
        public Task<Netio> Get()
            => Task<Netio>.Factory.StartNew(
                o => sendMessage((HttpRequestMessage) o),
                constructMessage());

        /// <summary>
        /// Perform a POST request to the Netio Json API setting the outputs to the desired state and return the
        /// controller state. 
        /// </summary>
        /// <param name="os">The desired state of the outputs.</param>
        /// <remarks>This function simply calls <see cref="sendMessage"/> with the result of
        /// <see cref="constructMessage(IEnumerable{Output})"/>.</remarks>
        /// <returns>The return value of <see cref="sendMessage"/>, identical to <see cref="Socket"/></returns>
        private Task<Netio> Post(IEnumerable<Output> os)
            => Task<Netio>.Factory.StartNew(
                o => sendMessage((HttpRequestMessage) o),
                constructMessage(os));

        /// <summary>
        /// Same as <see cref="Post(IEnumerable{heliomaster.Netio.Output})"/> but for a single <see cref="Output"/>.
        /// </summary>
        private Task<Netio> Post(Output o) => Post(new[] {o});

        /// <summary>
        /// Send a command to control a socket using the Netio Json API.
        /// </summary>
        /// <remarks>Consult the Netio Json API manual for details on the meaning of the parameters.</remarks>
        /// <param name="id">The id of the desired socket.</param>
        /// <param name="s">The desired state of the socket.</param>
        /// <param name="a">The desired action.</param>
        /// <param name="delay">The desired delay.</param>
        /// <returns>The result of <see cref="Post(Output)"/>, identical to <see cref="Socket"/>.</returns>
        public Task<Netio> Command(int id, States s = States.Off, OutputActions a = OutputActions.Ignore, int delay = 0)
            => Post(new Output {
                ID     = id,
                State  = s,
                Action = a,
                Delay  = delay
            });
        
        // public Task<Netio> Command(IEnumerable<int> _ids, IEnumerable<States> _states,
        //                            IEnumerable<OutputActions> _actions, IEnumerable<int> _delays) {
        //     var ids     = new List<int>(_ids);
        //     var states  = new List<States>(_states);
        //     var actions = new List<OutputActions>(_actions);
        //     var delays  = new List<int>(_delays);
        //
        //     return Post(ids.Select((t, i) => new Output {
        //         ID     = t,
        //         State  = i < states.Count ? states[i] : States.Off,
        //         Action = i < actions.Count ? actions[i] : OutputActions.Ignore,
        //         Delay  = i < delays.Count ? delays[i] : 0
        //     }).ToList());
        // }

        /// <summary>
        /// Extract the data for the socket with the given id from the given Netio controller state.
        /// </summary>
        /// <param name="n">The Netio controller state.</param>
        /// <param name="id">The id of the desired socket.</param>
        /// <returns></returns>
        private Output pick(Netio n, int id) {
            return n?.Outputs.FirstOrDefault(o => o.ID == id);
        }

        [XmlIgnore] private ObservableCollection<string> _names = new ObservableCollection<string>();
        
        /// <summary>
        /// The names assigned to the sockets in the Netio control panel.
        /// </summary>
        /// <remarks>If the collection is empty and a get is attempted, the accessor tries to populate it by requesting
        /// a <see cref="Socket"/> object (which might lead to a request for one).</remarks>
        [XmlIgnore] public override ObservableCollection<string> Names {
            get {
                if (_names.Count == 0) {
                    var names = Socket?.Outputs?.Select(o => o.Name);
                    if (names != null) foreach (var name in names) _names.Add(name);
                }

                return _names;
            }
            protected set {
                _names = value;
                OnPropertyChanged();
            }
        }

        /// <inheritdoc />
        /// <remarks>Checks the connection by requesting the current state.</remarks>
        public override bool Available {
            get {
                var t = Get();
                try {
                    t.Wait();
                    return t.Result != null;
                } catch { return false; }
            }
        }

        /// <summary>
        /// A dictionary linking the objects registered using <see cref="Register"/> to Netio id's.
        /// </summary>
        private readonly Dictionary<object, int> registry = new Dictionary<object, int>();

        /// <inheritdoc />
        public override bool Register(object o, string name) {
            if (name == null) return false;
            name = name.ToLower();
            var id = Socket?.Outputs?.FirstOrDefault(output => output.Name.ToLower() == name)?.ID;
            if (id != null) {
                registry[o] = (int) id;
                return true;
            } else return false;
        }

        // public bool Registered(object o) => registry.ContainsKey(o);
        
        /// <summary>
        /// Get the socket id that corresponds to a given registered object or null if not registered.
        /// </summary>
        /// <param name="o">The object whose id to retrieve.</param>
        public int? GetID(object o) => registry.ContainsKey(o) ? registry[o] : (int?) null;
        
        /// <summary>
        /// Get the socket id that corresponds to a given registered object or throw an error if not registered.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <exception cref="ArgumentException">If <paramref name="o"/> is not registered</exception>
        private int checkRegistered(object o)
            => GetID(o)
               ?? throw new ArgumentException("The object is not registered on the device.");

        /// <summary>
        /// Common interface for controlling a registered object using the Netio Json API.
        /// </summary>
        /// <param name="o">The object to control.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="delay">The delay associated to <see cref="OutputActions.OnDelay"/> or
        /// <see cref="OutputActions.OffDelay"/>.</param>
        /// <returns>The current power status of the device after the operation.</returns>
        private Task<PowerStatus> control(object o, OutputActions action, int delay = 0) {
            return Task<PowerStatus>.Factory.StartNew(() => {
                var id = checkRegistered(o);
                var res = pick(Command(id, States.On, action, delay).Result, id);
                return new PowerStatus {
                    On = res == null ? (bool?) null : (res.State == States.On)
                };
            });
        }

        /// <inheritdoc />
        public override Task<PowerStatus> On(object o) => control(o, OutputActions.TurnOn);

        /// <inheritdoc />
        public override Task<PowerStatus> Off(object o) => control(o, OutputActions.TurnOff);

        /// <inheritdoc />
        public override Task<PowerStatus> Toggle(object o) => control(o, OutputActions.Toggle);

        /// <inheritdoc />
        public override Task<PowerStatus> Reset(object o, TimeSpan? dt = null)
            => control(o, OutputActions.OffDelay, (int?) dt?.TotalMilliseconds ?? 0);

        /// <inheritdoc />
        public override Task<PowerStatus> Pulse(object o, TimeSpan? dt = null)
            => control(o, OutputActions.OnDelay, (int?) dt?.TotalMilliseconds ?? 0);
    }
}
