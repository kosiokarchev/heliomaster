using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Python.Runtime;

namespace heliomaster {
    public static class PythonExtensions {
        public static int[] shape(this CameraImage img) {
            return new [] {img.Height, img.Width, img.Channels};
        }
        public static string dtype(this CameraImage img) {
            return img.Depth == BitDepth.depth8  ? "uint8" :
                   img.Depth == BitDepth.depth16 ? "uint16" :
                   img.Depth == BitDepth.depth32 ? "uint32" : null;
        }
        public static unsafe dynamic to_numpy(this CameraImage img) {
            var ptr = (UInt64) img.raw;
            var shape = img.shape();
            var dtype = img.dtype();
            dynamic ret = null;
            Py.Run(() => {
                ret = Py.lib.pointer_to_ndarray(ptr, shape, dtype);
            });
            return ret;
        }

//        public static CameraImage load(string fname) {
//            Image.FromFile(fname).
//        }
    }

    public static class PythonGeneralExtensions {
        public static object ToCLI(this PyObject o) {
            if (PySequence.IsSequenceType(o)) {
                var list = new List<object>();
                foreach (PyObject subo in o)
                    list.Add(subo.ToCLI());
                return list;
            }
            if (PyString.IsStringType(o)) return o.AsManagedObject(typeof(string));
            if (PyInt.IsIntType(o)) return o.AsManagedObject(typeof(long));
            if (PyLong.IsLongType(o)) return o.AsManagedObject(typeof(long));
            if (PyFloat.IsFloatType(o)) return o.AsManagedObject(typeof(double));
            return o;
        }
    }
}
