namespace heliomaster {
    public partial class Observatory {
        /// <summary>
        /// Unifies the interface to notify the user of error occurring during observatory automation.
        /// </summary>
        /// <param name="e">The exception that has occurred.</param>
        /// <remarks> <see cref="Inform(string)"/>s the user of the formatted error and logs it at the appropriate
        /// logging level. Additionally, it the exception is more severe than a warning, i.e. a
        /// <see cref="AutoOperationsException"/> or a <see cref="CriticalObservatoryError"/>, shows a
        /// see cref="MessageBox"/> with the error message.</remarks>
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

        /// <summary>
        /// A collection of the messages produced by the automatic procedures. Shown to the user in a log-like list.
        /// </summary>
        public ObservableConcurrentList<ObservatoryMessage> Messages { get; } =
            new ObservableConcurrentList<ObservatoryMessage>();

        private ObservatoryMessage _lastMessage;
        /// <summary>
        /// A property which both serves to point out the last message if it is needed (getter), and to add a new
        /// message to <see cref="Messages"/> (setter).
        /// </summary>
        public ObservatoryMessage LastMessage {
            get => _lastMessage;
            set {
                if (_lastMessage == value) return; // reference equality
                _lastMessage = value;
                Messages.Add(_lastMessage);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Put the message on the <see cref="Messages"/> list by assigning it to <see cref="LastMessage"/>.
        /// </summary>
        /// <param name="msg">The <see cref="ObservatoryMessage"/> to inform the user of.</param>
        public void Inform(ObservatoryMessage msg) { LastMessage = msg; }
        /// <summary>
        /// Inform the user of a new message containing <paramref name="msg"/> emitted at this instant in time.
        /// </summary>
        /// <param name="msg">The text of the <see cref="ObservatoryMessage"/> to inform the user of.</param>
        public void Inform(string msg) { Inform(new ObservatoryMessage(msg)); }
    }
}
