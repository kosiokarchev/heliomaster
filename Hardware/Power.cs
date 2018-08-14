﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Xml.Serialization;
using heliomaster_wpf.Annotations;
using Newtonsoft.Json;

namespace heliomaster_wpf {
    public class PowerStatus {
        public bool? On;
    }

    public abstract class BasePower : BaseNotify {
        [XmlIgnore] public abstract ObservableCollection<string> Names { get; }
        [XmlIgnore] public abstract bool Available { get; }
        public abstract bool Register(object o, string name);

        public abstract Task<PowerStatus> On(object o);
        public abstract Task<PowerStatus> Off(object o);
        public abstract Task<PowerStatus> Toggle(object o);
        public abstract Task<PowerStatus> Reset(object o, TimeSpan dt);
        public abstract Task<PowerStatus> Pulse(object o, TimeSpan dt);
    }

    public enum PowerTypes {
        Basic, Netio
    }
}

namespace heliomaster_wpf.Netio {
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
        public string        Name { internal get; [UsedImplicitly] set; }
        public States        State;
        public OutputActions Action;
        public int           Delay;

        public bool ShouldSerializeDelay() => Action == OutputActions.OffDelay || Action == OutputActions.OffDelay;
//        [JsonIgnore] public double        Current, PowerFactor, Load, Energy;
    }
    public class Netio {
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

        [UsedImplicitly]
        public string EncryptedPassword {
            get => Pass.EncryptString();
            set => Pass = value.DecryptString();
        }

        [XmlIgnore] private Netio _socket;
        [XmlIgnore] public Netio Socket {
            get => _socket ?? (_socket = Get().Result);
            set => _socket = value;
        }

        private Uri uri => uriBuilder.Uri;
        private readonly HttpClient hc = new HttpClient();

        private readonly HttpRequestMessage _msg = new HttpRequestMessage { Method = HttpMethod.Post };
        private HttpRequestMessage get_msg(IEnumerable<Output> os) {
            _msg.RequestUri            = uri;
            _msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{new NetworkCredential(User, Pass).Password}")));
            _msg.Content = new StringContent(JsonConvert.SerializeObject(new Netio {Outputs = os}));
            Logger.debug("NETIO: Sending: " + _msg.Content.ReadAsStringAsync().Result);
            return _msg;
        }

        private void cleanPassword() {
            _msg.Headers.Authorization = null;
        }

        public Task<Netio> Get() {
            return Task<Netio>.Factory.StartNew(() => {
                var json = "";
                Utilities.InsecureSSL(() => {
                    try { json = hc.GetStringAsync(uri).Result; }
                    catch {}
                }).Wait();
                Logger.debug($"NETIO: Received: \"{json}\"");
                try { Socket = JsonConvert.DeserializeObject<Netio>(json); }
                catch { Socket = null; }
                return _socket;
            });
        }

        private Task<Netio> send(IReadOnlyCollection<Output> os) {
            return Task<Netio>.Factory.StartNew(() => {
                HttpResponseMessage response = null;
                Utilities.InsecureSSL(() => { response = hc.SendAsync(get_msg(os)).Result; }).Wait();
                cleanPassword();

                if (response?.StatusCode == HttpStatusCode.OK) {
                    var resp = response.Content.ReadAsStringAsync().Result;
                    Socket = JsonConvert.DeserializeObject<Netio>(resp);
                    return Socket;
                } else return null;
            });
        }
        private Task<Netio> send(Output o) => send(new[] {o});

        public Task<Netio> Command(int id, States s = States.Off, OutputActions a = OutputActions.Ignore, int delay = 0)
            => send(new Output {
                ID     = id,
                State  = s,
                Action = a,
                Delay  = delay
            });

        public Task<Netio> Command(IEnumerable<int> _ids, IEnumerable<States> _states,
                                   IEnumerable<OutputActions> _actions, IEnumerable<int> _delays) {
            var ids     = new List<int>(_ids);
            var states  = new List<States>(_states);
            var actions = new List<OutputActions>(_actions);
            var delays  = new List<int>(_delays);

            return send(ids.Select((t, i) => new Output {
                ID     = t,
                State  = i < states.Count ? states[i] : States.Off,
                Action = i < actions.Count ? actions[i] : OutputActions.Ignore,
                Delay  = i < delays.Count ? delays[i] : 0
            }).ToList());
        }

        private Output pick(Netio n, int id) {
            return n?.Outputs.FirstOrDefault(o => o.ID == id);
        }

        [XmlIgnore] private readonly ObservableCollection<string> _names = new ObservableCollection<string>();
        [XmlIgnore] public override ObservableCollection<string> Names {
            get {
                if (_names.Count == 0) {
                    var names = Socket?.Outputs.Select(o => o.Name);
                    if (names != null) foreach (var name in names) _names.Add(name);
                }

                return _names;
            }
        }
        public override bool Available {
            get {
                var t = Get();
                t.Wait();
                return t.Exception == null && t.Result != null;
            }
        }

        private readonly Dictionary<object, int> registry = new Dictionary<object, int>();

        public override bool Register(object o, string name) {
            name = name.ToLower();
            var id = Socket?.Outputs?.FirstOrDefault(output => output.Name.ToLower() == name)?.ID;
            if (id != null) {
                registry.Add(o, (int) id);
                return true;
            } else return false;
        }

        public bool Registered(object o) => registry.ContainsKey(o);
        private int checkRegistered(object o) {
            if (!Registered(o))
                throw new ArgumentException("The object is not registered on the device.");
            return registry[o];
        }

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

        public override Task<PowerStatus> Reset(object o, TimeSpan dt) {
            return control(o, OutputActions.OffDelay, (int) dt.TotalMilliseconds);
        }

        public override Task<PowerStatus> Pulse(object o, TimeSpan dt) {
            return control(o, OutputActions.OnDelay, (int) dt.TotalMilliseconds);
        }
    }
}
