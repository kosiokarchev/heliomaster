using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using heliomaster.Annotations;

namespace heliomaster {
    public class HMWindow : Window, INotifyPropertyChanged {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public bool IsClosed { get; private set; }
        protected override void OnClosed(EventArgs e) {
            IsClosed = true;
            base.OnClosed(e);
        }

        protected void TryCommand(Action cmd) {
            try { cmd(); } catch (Exception) { }
        }
    }
}
