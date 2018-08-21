using System;
using System.Collections;
using System.Collections.Generic;

namespace heliomaster {
    public static class PythonExtensions {
        public static ArrayList shape(this CameraImage img) {
            return new ArrayList(new [] {img.Height, img.Width, img.Channels});
        }
        public static string dtype(this CameraImage img) {
            return img.Depth == BitDepth.depth8  ? "uint8" :
                   img.Depth == BitDepth.depth16 ? "uint16" :
                   img.Depth == BitDepth.depth32 ? "uint32" : null;
        }
        public static unsafe dynamic to_numpy(this CameraImage img) {
            dynamic res = null;
            var ptr = (IntPtr) img.raw;
            var shape = img.shape();
            var dtype = img.dtype();
            Python.Run(() => res = Python.lib.pointer_to_ndarray(ptr, shape, dtype));
            return res;
        }
    }
}
