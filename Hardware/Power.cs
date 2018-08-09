using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using heliomaster_wpf.Annotations;

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
    public class NetioPower : Power, INotifyPropertyChanged {
        private readonly Dictionary<object, int> Registry = new Dictionary<object, int>();

        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
}
