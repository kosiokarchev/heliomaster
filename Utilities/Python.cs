using System;
using System.Collections.Generic;
using Python.Runtime;

namespace heliomaster_wpf {
    public static class Python {
        public static dynamic np;
        public static dynamic ephem;

        public static class Objects {
            public static dynamic Sun;
            public static dynamic Moon;
        }

        public static void Start() {
            var t = DateTime.Now;
            PythonEngine.Initialize();

            Run(() => {
                np = Py.Import("numpy");
                ephem = Py.Import("ephem");
                Objects.Sun = ephem.Sun;
                Objects.Moon = ephem.Moon;
            });

            Console.WriteLine($"Python started in {(DateTime.Now - t).TotalSeconds}s");
        }

        public static void Run(Action a) {
            using (Py.GIL()) a();
        }
    }

    public static class Pynder {
        public static double rad2hours   = 12 / Math.PI;
        public static double rad2degrees = 180 / Math.PI;

        public struct Coords {
            public double ra, dec;
        }

        public enum Objects {
            Sun, Moon
        }
        public static readonly Dictionary<Objects, dynamic> ObjectClasses = new Dictionary<Objects, dynamic> {
            {Objects.Sun, Python.Objects.Sun},
            {Objects.Moon, Python.Objects.Moon}
        };

        private static dynamic _obs;
        public static dynamic obs {
            get {
                if (_obs == null) Python.Run(() => { _obs = Python.ephem.city("Madrid"); });
                return _obs;
            }
        }

        public static Coords find(Objects obj) {
            var ret = new Coords();
            Python.Run(() => {
                obs.date = Python.ephem.now();
                var data = ObjectClasses[obj](obs);
                ret.ra  = data.ra  * rad2hours;
                ret.dec = data.dec * rad2degrees;
            });
            return ret;
        }
    }
}
