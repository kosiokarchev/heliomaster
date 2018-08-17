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

namespace heliomaster {
    public class PowerStatus {
        public bool? On;
    }

    public abstract class BasePower : BaseNotify {
        [XmlIgnore] public abstract ObservableCollection<string> Names { get; protected set; }
        [XmlIgnore] public abstract bool Available { get; }
        public abstract bool Register(object o, string name);

        public abstract Task<PowerStatus> On(object o);
        public abstract Task<PowerStatus> Off(object o);
        public abstract Task<PowerStatus> Toggle(object o);
        public abstract Task<PowerStatus> Reset(object o, TimeSpan? dt = null);
        public abstract Task<PowerStatus> Pulse(object o, TimeSpan? dt = null);
    }

    public enum PowerTypes {
        Basic, Netio
    }
}

namespace heliomaster.Netio {
    public enum States {
        Off = 0,
        On  = 1
    }
    public enum OutputActions {
        TurnOff  = 0,
        TurnOn   = 1,
        OffDelay = 2,
        OnDelay  = 3,
        Toggle   = 4,
        None     = 5,
        Ignore   = 6,
    }

    [UsedImplicitly] public class Agent {
        public string   Model,    Version, JSONVer, DeviceName, MAC;
        public int      VendorID, OemID,   Uptime,  NumOutputs;
        public DateTime Time;
    }
    [UsedImplicitly] public class GlobalMeasure {
        public double   Voltage, Frequency, TotalCurrent, OverallPowerFactor, TotalLoad, TotalEnergy;
        public DateTime EnergyStart;
    }
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
    public class NetioSocket {
        [JsonIgnore] public Agent                Agent;
        [JsonIgnore] public GlobalMeasure        GlobalMeasure;
        public              IEnumerable<Output>  Outputs;
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class Power : BasePower {
        private readonly UriBuilder uriBuilder = new UriBuilder {Path = "netio.json"};

        public string Host {
            get => uriBuilder.Host;
            set {
                if (value == uriBuilder.Host) return;
                uriBuilder.Host = value;
                OnPropertyChanged();
            }
        }
        public int Port {
            get => uriBuilder.Port == -1 ? (UseHttps ? 443 : 80) : uriBuilder.Port;
            set {
                if (value == uriBuilder.Port) return;
                uriBuilder.Port = value;
                OnPropertyChanged();
            }
        }
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

        private string _user;
        public string User {
            get => _user;
            set {
                if (value == uriBuilder.UserName) return;
                _user = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore] public int UseHttpsIndex {
            get => UseHttps ? 1 : 0;
            set => UseHttps = value == 1;
        }

        [XmlIgnore] public SecureString Pass { get; set; } = new SecureString();
        [XmlIgnore] public string PassString {
            get => Pass.ToInsecureString();
            set => Pass = value.ToSecureString();
        }
        public string EncryptedPassword {
            get => Pass.EncryptString();
            set => Pass = value.DecryptString();
        }

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
        [XmlIgnore] private NetioSocket _socket;
        [XmlIgnore] public NetioSocket Socket {
            get => _socket ?? (faulted ? null : _socket = Get().Result);
            set {
                _socket = value;
                faulted = value == null;
                Names = new ObservableCollection<string>();
            }
        }

        private Uri uri => uriBuilder.Uri;
        private readonly HttpClient hc = new HttpClient();

        private HttpRequestMessage get_msg(IEnumerable<Output> os = null) {
            var ret = new HttpRequestMessage {
                Method     = os == null ? HttpMethod.Get : HttpMethod.Post,
                RequestUri = uri,
                Headers = {
                    Authorization =
                        new AuthenticationHeaderValue(
                            "Basic",
                            Convert.ToBase64String(
                                Encoding.ASCII.GetBytes($"{User}:{new NetworkCredential(User, Pass).Password}")))
                }
            };
            if (os != null) {
                var json = JsonConvert.SerializeObject(new NetioSocket {Outputs = os});
                ret.Content = new StringContent(json);
                Logger.debug($"NETIO: Sending: {json}");
            }
            return ret;
        }


        private SemaphoreSlim sem = new SemaphoreSlim(1, 1);
        private NetioSocket sendMessage(HttpRequestMessage msg) {
            try {
                HttpResponseMessage response = null;

                var token = new CancellationTokenSource(Timeout);
                sem.Wait();
                Utilities.InsecureSSL(() => {
                    try {
                        var task = hc.SendAsync(msg, HttpCompletionOption.ResponseContentRead, token.Token);
                        task.Wait(token.Token);
                        if (task.Status == TaskStatus.RanToCompletion)
                            response = task.Result;
                    } catch (OperationCanceledException e) {
                        Logger.debug($"NETIO: Operation timed out: {e.Message}");
                    } catch (Exception e) {
                        Logger.debug($"NETIO: Error in HttpClient: {e.Message}");
                    }
                }).Wait(token.Token);
                sem.Release();

                if (response.IsSuccessStatusCode) {
                    var json = response?.Content.ReadAsStringAsync().Result;
                    Logger.debug($"NETIO: Received: \"{json}\"");
                    Socket = JsonConvert.DeserializeObject<NetioSocket>(json);
                } else {
                    Logger.debug($"NETIO: Got bad response: {response.StatusCode}");
                    Socket = null;
                }
            } catch (Exception e) {
                Logger.debug($"NETIO: Error in get: {e.GetType().Name}: {e.Message}");
                Socket = null;
            }

            return _socket;
        }

        public Task<NetioSocket> Get()
            => Task<NetioSocket>.Factory.StartNew(
                o => sendMessage((HttpRequestMessage) o),
                get_msg());

        private Task<NetioSocket> Post(IEnumerable<Output> os)
            => Task<NetioSocket>.Factory.StartNew(
                o => sendMessage((HttpRequestMessage) o),
                get_msg(os));

        private Task<NetioSocket> Post(Output o) => Post(new[] {o});

        public Task<NetioSocket> Command(int id, States s = States.Off, OutputActions a = OutputActions.Ignore, int delay = 0)
            => Post(new Output {
                ID     = id,
                State  = s,
                Action = a,
                Delay  = delay
            });

        public Task<NetioSocket> Command(IEnumerable<int> _ids, IEnumerable<States> _states,
                                   IEnumerable<OutputActions> _actions, IEnumerable<int> _delays) {
            var ids     = new List<int>(_ids);
            var states  = new List<States>(_states);
            var actions = new List<OutputActions>(_actions);
            var delays  = new List<int>(_delays);

            return Post(ids.Select((t, i) => new Output {
                ID     = t,
                State  = i < states.Count ? states[i] : States.Off,
                Action = i < actions.Count ? actions[i] : OutputActions.Ignore,
                Delay  = i < delays.Count ? delays[i] : 0
            }).ToList());
        }

        private Output pick(NetioSocket n, int id) {
            return n?.Outputs.FirstOrDefault(o => o.ID == id);
        }

        [XmlIgnore] private ObservableCollection<string> _names = new ObservableCollection<string>();
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
        public override bool Available {
            get {
                var t = Get();
                try {
                    t.Wait();
                    return t.Result != null;
                } catch { return false; }
            }
        }

        private readonly Dictionary<object, int> registry = new Dictionary<object, int>();

        public override bool Register(object o, string name) {
            name = name.ToLower();
            var id = Socket?.Outputs?.FirstOrDefault(output => output.Name.ToLower() == name)?.ID;
            if (id != null) {
                registry[o] = (int) id;
                return true;
            } else return false;
        }

        public bool Registered(object o) => registry.ContainsKey(o);
        public int? GetID(object o) => registry.ContainsKey(o) ? registry[o] : (int?) null;
        private int checkRegistered(object o)
            => GetID(o)
               ?? throw new ArgumentException("The object is not registered on the device.");

        private Task<PowerStatus> control(object o, OutputActions action, int delay = 0) {
            return Task<PowerStatus>.Factory.StartNew(() => {
                var id = checkRegistered(o);
                var res = pick(Command(id, States.On, action, delay).Result, id);
                return new PowerStatus {
                    On = res == null ? (bool?) null : (res.State == States.On)
                };
            });
        }

        public override Task<PowerStatus> On(object o) {
            return control(o, OutputActions.TurnOn);
        }

        public override Task<PowerStatus> Off(object o) {
            return control(o, OutputActions.TurnOff);
        }

        public override Task<PowerStatus> Toggle(object o) {
            return control(o, OutputActions.Toggle);
        }

        public override Task<PowerStatus> Reset(object o, TimeSpan? dt = null) {
            return control(o, OutputActions.OffDelay, (int?) dt?.TotalMilliseconds ?? 0);
        }

        public override Task<PowerStatus> Pulse(object o, TimeSpan? dt = null) {
            return control(o, OutputActions.OnDelay, (int?) dt?.TotalMilliseconds ?? 0);
        }
    }
}
