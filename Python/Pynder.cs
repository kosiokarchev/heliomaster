using System;
using System.Collections.Generic;

namespace heliomaster {
    /// <summary>
    /// A class to locate celestial objects using the <a href="https://rhodesmill.org/pyephem/">PyEphem</a> Python package.
    /// </summary>
    /// <remarks> This class represents the celestial objects it can locate in two ways. The back-facing
    /// <see cref="PyEphemObjects"/> class contains references to the PyEphem classes used to construct objects with the
    /// <see cref="Observer"/> that calculate the desired parameters. On the other hand the enumeration
    /// <see cref="RegisteredObject"/> is intended for use by the front-end to e.g. allow the user to choose among these
    /// objects. </remarks>
    public static class Pynder {
        /// <summary>
        /// A container for PyEphem celestial bodies.
        /// </summary>
        public static class PyEphemObjects {
            /// <summary>
            /// The <c>ephem.Sun</c> class that can be instantiated with an observer to calculate the Sun's parameters. 
            /// </summary>
            public static dynamic Sun;
            /// <summary>
            /// The <c>ephem.Moon</c> class that can be instantiated with an observer to calculate the Moon's parameters. 
            /// </summary>
            public static dynamic Moon;

            /// <summary>
            /// Access <see cref="Py.ephem"/> and retrieve the classes representing celestial objects if
            /// <see cref="Py.Running"/> or delete these references if it is not.
            /// </summary>
            public static void Initialize() {
                if (Py.Running) {
                    Sun  = Py.ephem.Sun;
                    Moon = Py.ephem.Moon;
                } else {
                    Sun  = null;
                    Moon = null;
                }
            }
        }
        
        /// <summary>
        /// A utility struct containing right ascension and declination and allows them to be returned simultaneously
        /// by <see cref="Pynder.Find"/>.
        /// </summary>
        public struct Coords {
            public double ra, dec;
        }

        /// <summary>
        /// An enumeration containing the celestial objects that <see cref="Pynder"/> can locate.
        /// </summary>
        public enum RegisteredObject {
            Sun, Moon
        }

        /// <summary>
        /// A mapping between <see cref="RegisteredObject"/>s and the PyEphem classes saved in <see cref="PyEphemObjects"/>.
        /// </summary>
        private static readonly Dictionary<RegisteredObject, dynamic> ObjectPyObjects = new Dictionary<RegisteredObject, dynamic> {
            {RegisteredObject.Sun, PyEphemObjects.Sun},
            {RegisteredObject.Moon, PyEphemObjects.Moon}
        };

        private static dynamic _observer;
        /// <summary>
        /// The <c>ephem.Observer</c> to use in calculations. Defaults to the city of Madrid.
        /// </summary>
        public static dynamic Observer {
            get {
                if (_observer == null) Py.Run(() => { _observer = Py.ephem.city("Madrid"); });
                return _observer;
            }
        }

        /// <summary>
        /// Calculate and return the apparent right ascension and declination of the given object for the <see cref="Observer"/>.
        /// </summary>
        /// <param name="obj">The identifier for the object to locate.</param>
        /// <returns>A <see cref="Coords"/> struct filled with the right ascension and declination of the object.</returns>
        /// <exception cref="NoPythonException">If Python is not available.</exception>
        public static Coords Find(RegisteredObject obj) {
            if (!Py.Running)
                throw new NoPythonException();

            var ret = new Coords();
            Py.Run(() => {
                Observer.date = Py.ephem.now();
                var data = ObjectPyObjects[obj](Observer);
                ret.ra  = data.ra * 12 / Math.PI;   // radians -> hours
                ret.dec = data.dec * 180 / Math.PI; // radians -> degrees
            });
            return ret;
        }
    }
}
