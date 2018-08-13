using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace heliomaster_wpf {
    public class PowerStatus {
        public bool On;
    }

    public abstract class Power {
        public abstract void Register(object o, string name);

        public abstract Task<PowerStatus> On(object o);
        public abstract Task<PowerStatus> Off(object o);
        public abstract Task<PowerStatus> Toggle(object o);
        public abstract Task<PowerStatus> Reset(object o, TimeSpan dt);
        public abstract Task<PowerStatus> Pulse(object o, TimeSpan dt);
    }
}

namespace heliomaster_wpf.Netio {
    public static class JsonConvertEx {
        public static string SerializeObject<T>(T value) {
            StringBuilder sb = new StringBuilder(256);
            StringWriter  sw = new StringWriter(sb, CultureInfo.InvariantCulture);

            var jsonSerializer = JsonSerializer.CreateDefault();
            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw)) {
                jsonWriter.Formatting  = Formatting.Indented;
                jsonWriter.IndentChar  = ' ';
                jsonWriter.Indentation = 3;

                jsonSerializer.Serialize(jsonWriter, value, typeof(T));
            }

            return sw.ToString();
        }
    }


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

    public class Agent {
        public string   Model,    Version, JSONVer, DeviceName, MAC;
        public int      VendorID, OemID,   Uptime,  NumOutputs;
        public DateTime Time;
    }

    public class GlobalMeasure {
        public double   Voltage, Frequency, TotalCurrent, OverallPowerFactor, TotalLoad, TotalEnergy;
        public DateTime EnergyStart;
    }

    public class Output {
        public              int           ID;
        [JsonIgnore] public string        Name;
        public              States        State;
        public              OutputActions OutputAction;
        public              int           Delay;
        [JsonIgnore] public double        Current, PowerFactor, Load, Energy;
    }

    public class Netio {
        [JsonIgnore] public Agent         Agent;
        [JsonIgnore] public GlobalMeasure GlobalMeasure;
        public              List<Output>  Outputs;
    }

    public class Power : BaseNotify {
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
            get => uriBuilder.Port;
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
            }
        }

        public string User {
            get => uriBuilder.UserName;
            set {
                if (value == uriBuilder.UserName) return;
                uriBuilder.UserName = value;
                OnPropertyChanged();
            }
        }

        private SecureString _pass;

        public SecureString Pass {
            get => _pass;
            set {
                if (Equals(value, _pass)) return;
                _pass = value;
                OnPropertyChanged();
            }
        }

        private Uri uri {
            get {
                uriBuilder.Password = new NetworkCredential("", Pass).Password;
                return uriBuilder.Uri;
            }
        }

        public Netio Socket;

        private readonly WebClient wc = new WebClient();

        public async Task<Netio> Get() {
            var json = "";
            await Utilities.InsecureSSL(() => {
                json                = wc.DownloadString(uri);
                uriBuilder.Password = "";
            });
            Socket = JsonConvert.DeserializeObject<Netio>(json);
            return Socket;
        }

        private async Task<Netio> send(List<Output> os) {
            var s = new StringBuilder();
            foreach (var o in os) {
                s.Append($"output{o.ID}={(int) o.OutputAction}&delay{o.ID}={o.Delay}&");
            }

            await Utilities.InsecureSSL(() => {
                MessageBox.Show(wc.DownloadString("https://10.66.180.60/netio.cgi?pass=kosio&output1=4"));
            });

            var json = JsonConvertEx.SerializeObject(new Netio {Outputs = os});

            Console.WriteLine(json);

            var response = "";
            await Utilities.InsecureSSL(() => {
                response = Encoding.ASCII.GetString(wc.UploadData(uri, Encoding.ASCII.GetBytes(json)));
//                response = wc.UploadString(uri, "POST", json);
                uriBuilder.Password = "";
            });

            Socket = JsonConvert.DeserializeObject<Netio>(response);
            MessageBox.Show(response);
            return Socket;
        }

        private Task<Netio> send(Output o) => send(new List<Output>(new[] {o}));

        public Task<Netio>
            Command(int id, States s = States.Off, OutputActions a = OutputActions.Ignore, int delay = 0) => send(
            new Output {
                ID           = id,
                State        = s,
                OutputAction = a,
                Delay        = delay
            });

        public Task<Netio> Command(IEnumerable<int> _ids, IEnumerable<States> _states,
                                   IEnumerable<OutputActions> _actions, IEnumerable<int> _delays) {
            var ids     = new List<int>(_ids);
            var states  = new List<States>(_states);
            var actions = new List<OutputActions>(_actions);
            var delays  = new List<int>(_delays);

            return send(ids.Select((t, i) => new Output {
                ID           = t,
                State        = i < states.Count ? states[i] : States.Off,
                OutputAction = i < actions.Count ? actions[i] : OutputActions.Ignore,
                Delay        = i < delays.Count ? delays[i] : 0
            }).ToList());
        }
    }
}
