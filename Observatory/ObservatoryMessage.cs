using System;

namespace heliomaster {
    /// <summary>
    /// A class (structure) that holds information for messages emitted during the automatic operations of the observatory.
    /// </summary>
    public class ObservatoryMessage {
        /// <summary>
        /// The text of the message.
        /// </summary>
        public string Message { get; }
        /// <summary>
        /// The time the message was emitted.
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        /// Create a new ObservatoryMessage
        /// </summary>
        /// <param name="msg">The text of the message, assigned to <see cref="Message"/>.</param>
        /// <param name="time">The time the message was emitted, assigned to <see cref="Time"/>.</param>
        public ObservatoryMessage(string msg, DateTime time) {
            Message = msg;
            Time = time;
        }
        /// <summary>
        /// Create a new Observatory message emitted at the present moment.
        /// </summary>
        /// <param name="msg">The text of the message, assigned to <see cref="Message"/>.</param>
        public ObservatoryMessage(string msg) : this(msg, DateTime.Now) { }
    }
}
