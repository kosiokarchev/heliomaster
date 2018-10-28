namespace heliomaster {
    public partial class Observatory {
        public void Emit(ObservatoryException e) {
            // TODO: Error handling, duh...
            Inform(Utilities.FormatException(e));
            if (e is ObservatoryWarning w)
                Logger.warning(e.Message);
            else if (e is AutoOperationsException) {
                Logger.error(e.Message);
                MessageBox.Show(e.Message);
            } else if (e is CriticalObservatoryError) {
                Logger.critical(e.Message);
                MessageBox.Show(e.Message);
            }
        }

        public ObservableConcurrentList<ObservatoryMessage> Messages { get; } =
            new ObservableConcurrentList<ObservatoryMessage>();

        private ObservatoryMessage _lastMessage;

        public ObservatoryMessage LastMessage {
            get => _lastMessage;
            set {
                if (_lastMessage == value) return; // reference equality
                _lastMessage = value;
                Messages.Add(_lastMessage);
                OnPropertyChanged();
            }
        }

        public void Inform(ObservatoryMessage msg) { LastMessage = msg; }

        public void Inform(string msg) { Inform(new ObservatoryMessage(msg)); }
    }
}