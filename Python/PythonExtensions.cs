using System;
using System.Collections.Generic;
using Python.Runtime;

namespace heliomaster {
    /// <summary>
    /// A class containing extension functions that use Python.
    /// </summary>
    public static partial class PythonExtensions {
        /// <summary>
        /// Convert a Python object to a CLI object.
        /// </summary>
        /// <param name="o">The Python object to convert.</param>
        /// <returns>
        /// <list type="bullet">
        ///     <item>
        ///         <term><see cref="PySequence.IsSequenceType"/>(<paramref name="o"/>)</term>
        ///         <description>A <see cref="List{T}"/> of <see cref="Object"/> instances corresponding to
        ///         the converted members of the sequence.</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="PyString.IsStringType"/>(<paramref name="o"/>)</term>
        ///         <description>A <see cref="string"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="PyInt.IsIntType"/>(<paramref name="o"/>) or <see cref="PyLong.IsLongType"/>(o)</term>
        ///         <description>A <see cref="long"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="PyFloat.IsFloatType"/>(<paramref name="o"/>)</term>
        ///         <description>A <see cref="double"/>.</description>
        ///     </item>
        /// </list>
        /// </returns>
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

    // Image extensions
    public static partial class PythonExtensions {
        /// <summary>
        /// Get the dimensions of a <see cref="CameraImage"/> as a NumPy-style shape array (height, width, channels).
        /// </summary>
        /// <param name="img">The image whose shape to retrieve.</param>
        /// <returns>An array of three integers [<see cref="CameraImage.Height"/>, <see cref="CameraImage.Width"/>,
        /// <see cref="CameraImage.Channels"/>].</returns>
        public static int[] shape(this CameraImage img) {
            return new [] {img.Height, img.Width, img.Channels};
        }
        
        /// <summary>
        /// Get the NumPy-style dtype of the image as the representative string.
        /// </summary>
        /// <param name="img">The image whose dtype to retrieve.</param>
        /// <returns>"uint8", "uint16" or "uint32" depending on <see cref="CameraImage.Depth"/>.</returns>
        public static string dtype(this CameraImage img) {
            return img.Depth == BitDepth.depth8  ? "uint8" :
                   img.Depth == BitDepth.depth16 ? "uint16" :
                   img.Depth == BitDepth.depth32 ? "uint32" : null;
        }
        
        /// <summary>
        /// Return a NumPy array wrapping the image data.
        /// </summary>
        /// <param name="img">The image whose data to wrap.</param>
        /// <remarks>This method delegated to the <c>libhm.pointer_to_ndarray</c> function from HMPL.</remarks>
        public static unsafe dynamic to_numpy(this CameraImage img) {
            var ptr   = (UInt64) img.raw;
            var shape = img.shape();
            var dtype = img.dtype();
            
            return Py.Run(() => Py.lib.pointer_to_ndarray(ptr, shape, dtype));
        }
    }
}
