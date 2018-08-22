using System;

namespace heliomaster {
    public class ObservatoryMessage {
        public string Message { get; }
        public DateTime Time { get; }

        public ObservatoryMessage(string msg, DateTime time) {
            Message = msg;
            Time = time;
        }
        public ObservatoryMessage(string msg) : this(msg, DateTime.Now) { }
    }
}
