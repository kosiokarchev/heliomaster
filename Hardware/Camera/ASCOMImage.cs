using System;
using System.Runtime.InteropServices;

namespace heliomaster {
    public sealed class ASCOMImage : CameraImage {
        public ASCOMImage(Array data, int channels, BitDepth depth) : base(
            data.GetLength(0), data.GetLength(1), channels, depth) {
            Put(data);
        }
    }
}
