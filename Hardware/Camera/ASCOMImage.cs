using System;

namespace heliomaster {
    public sealed class ASCOMImage : CameraImage {
        /// <summary>
        /// Create a new <see cref="CameraImage"/> from an ASCOM <see cref="ASCOM.DeviceInterface.ICameraV2.ImageArray"/>.
        /// </summary>
        /// <param name="data">The <see cref="ASCOM.DeviceInterface.ICameraV2.ImageArray"/> to create an image from.</param>
        /// <param name="depth">The bit depth of the data. The <paramref name="data"/> array always consists of
        /// <see cref="Int32"/> entries, which will be cast to appropriate type.</param>
        public ASCOMImage(Array data, BitDepth depth) : base(
            data.GetLength(0), data.GetLength(1), data.Rank == 2 ? 1 : data.GetLength(2), depth) {
            Put(data);
        }
    }
}
