using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace heliomaster {
    /// <summary>
    /// Bit depths with values corresponding to the number of bytes of a channel in a pixel.
    /// </summary>
     public enum BitDepth {
        depth8  = 1,
        depth16 = 2,
        depth32 = 4
    }

    /// <summary>
    /// A class which contains, describes, and handles the data for an image.
    /// </summary>
    public unsafe class CameraImage {
        public enum ImageFileFormat {
            __fromExtension,
            png, jpeg, tiff,
        }
        
        /// <summary>
        /// Maps <see cref="ImageFileFormat"/>s to <see cref="BitmapEncoder"/> subclasses used to save images.
        /// </summary>
        private static readonly Dictionary<ImageFileFormat, Type> Encoders = new Dictionary<ImageFileFormat, Type> {
            {ImageFileFormat.png, typeof(PngBitmapEncoder)},
            {ImageFileFormat.jpeg, typeof(JpegBitmapEncoder)},
            {ImageFileFormat.tiff, typeof(TiffBitmapEncoder)},
        };

        /// <summary>
        /// Pointer to the image data as a C-style array (height, width, channels)
        /// </summary>
        public readonly void* raw;

        /// <summary>
        /// Width of the image in pixels.
        /// </summary>
        public readonly int Width;
        
        /// <summary>
        /// Height of the image in pixels.
        /// </summary>
        public readonly int Height;
        
        /// <summary>
        /// Number of channels of the image.
        /// </summary>
        public readonly int Channels;
        
        /// <summary>
        /// Bit depth of the image.
        /// </summary>
        public readonly BitDepth Depth;
        
        /// <summary>
        /// Length in elements of the data. 
        /// </summary>
        public int Length => Width * Height * Channels;
        
        /// <summary>
        /// Length in bytes of the data.
        /// </summary>
        public int Size => Length * (int) Depth;

        /// <summary>
        /// Create a new CameraImage with the given parameters.
        /// </summary>
        /// <remarks>Constructing a CameraImage automatically allocates a suitable buffer!</remarks>
        protected CameraImage(int width, int height, int channels, BitDepth depth) {
            Width    = width;
            Height   = height;
            Channels = channels;
            Depth    = depth;
            raw      = (void*) Marshal.AllocHGlobal(Size);
        }

        /// <summary>
        /// Used to serialize access to the image data.
        /// </summary>
        public readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();

        /// <summary>
        /// Called internally when the image data has been updated in order to reset the <see cref="BitmapSource"/> "preview".
        /// </summary>
        public void BufferUpdated() {
            _bitmapCache = null;
        }

        /// <summary>
        /// Replace the image data by that in <see cref="data"/>. Used to put ASCOM image data.
        /// </summary>
        /// <param name="data">An array of pixel values as in <see cref="ASCOM.DeviceInterface.ICameraV2.ImageArray"/>.
        /// Note that this means that the first index into the array is the <b>column</b> number, i.e. the dimensions
        /// are (width, height, channels).</param>
        /// <exception cref="ArgumentOutOfRangeException">When the <see cref="Depth"/> is invalid.</exception>
        public virtual void Put(Array data) {
            rwlock.EnterWriteLock();

            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);

            Int32 * d = (Int32*) gch.AddrOfPinnedObject();
            int     r = -1;

            switch (Depth) {
                case BitDepth.depth32:
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                ((UInt32*) raw)[++r] = (UInt32) d[j*Height*Channels + i*Channels + k];
                    break;
                case BitDepth.depth16:
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                ((UInt16*) raw)[++r] = (UInt16) d[j*Height*Channels + i*Channels + k];
                    break;
                case BitDepth.depth8:
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                ((byte*) raw)[++r] = (byte) d[j*Height*Channels + i*Channels + k];
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            gch.Free();

            rwlock.ExitWriteLock();

            BufferUpdated();
        }

        /// <summary>
        /// Copy the image along with its data.
        /// </summary>
        /// <returns>A new CameraImage with a separate data buffer that contains a copy of this image's data.</returns>
        public CameraImage Copy() {
            var ret = new CameraImage(Width, Height, Channels, Depth);

            rwlock.EnterReadLock();

            ret.rwlock.EnterWriteLock();
            for (var i = 0; i < Size; ++i)
                ((byte *) ret.raw)[i] = ((byte *) raw)[i];
            ret.rwlock.ExitWriteLock();

            ret._bitmapCache = _bitmapCache;
            rwlock.ExitReadLock();

            return ret;
        }

        /// <summary>
        /// Generate a <see cref="BitmapSource"/> from the current image data. 
        /// </summary>
        /// <remarks>Currently does not work with 32-bit images.</remarks>
        public BitmapSource GetBitmap() {
            rwlock.EnterReadLock();
            var img = BitmapSource.Create(
                Width, Height, dpiX: 96, dpiY: 96,
                pixelFormat: (Channels == 1) ?
                    ((Depth == BitDepth.depth8)  ? PixelFormats.Gray8 :
                     (Depth == BitDepth.depth16) ? PixelFormats.Gray16 :
                                                   PixelFormats.Gray32Float)
                    : ((Depth == BitDepth.depth8)  ? PixelFormats.Rgb24 :
                       (Depth == BitDepth.depth16) ? PixelFormats.Rgb48 :
                                                     PixelFormats.Rgb128Float),
                palette: null, buffer: (IntPtr) raw, bufferSize: Size,
                stride: Width * Channels * (int) Depth);
            img.Freeze();
            rwlock.ExitReadLock();
            return img;
        }

        private BitmapSource _bitmapCache;
        /// <summary>
        /// Retrieve a <see cref="BitmapSource"/> representation of the image.
        /// </summary>
        /// <remarks>The conversion is done via <see cref="GetBitmap"/> and cached in <see cref="_bitmapCache"/>
        /// until the buffer is updated and the cache cleared by <see cref="BufferUpdated"/>.</remarks>
        public BitmapSource BitmapSource => _bitmapCache ?? (_bitmapCache = GetBitmap());

        /// <summary>
        /// Return the image data copied in an <see cref="Array"/>. The inverse of <see cref="Put(Array)"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <see cref="Depth"/> is invalid.</exception>
        public Array Take() {
            Array data;
            GCHandle gch;
            int r = -1;

            rwlock.EnterReadLock();

            switch (Depth) {
                case BitDepth.depth32:
                    data = new UInt32[Height, Width];
                    gch = GCHandle.Alloc(data, GCHandleType.Pinned);
                    UInt32 * d32 = (UInt32*) gch.AddrOfPinnedObject();
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                d32[j*Height*Channels + i*Channels + k] = ((UInt32*) raw)[++r];
                    break;
                case BitDepth.depth16:
                    data = new UInt16[Height, Width];
                    gch  = GCHandle.Alloc(data, GCHandleType.Pinned);
                    UInt16 * d16 = (UInt16*) gch.AddrOfPinnedObject();
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                d16[j*Height*Channels + i*Channels + k] = ((UInt16*) raw)[++r];
                    break;
                case BitDepth.depth8:
                    data = new byte[Height, Width];
                    gch  = GCHandle.Alloc(data, GCHandleType.Pinned);
                    byte * d8 = (byte*) gch.AddrOfPinnedObject();
                    for (var i = 0; i < Height; ++i)
                        for (var j = 0; j < Width; ++j)
                            for (var k=0; k<Channels; ++k)
                                d8[j*Height*Channels + i*Channels + k] = ((byte*) raw)[++r];
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            rwlock.ExitReadLock();

            gch.Free();

            return data;
        }


        /// <summary>
        /// Save the image to a file in the given format, optionally applying a given transformation.
        /// </summary>
        /// <param name="fname">The filename to save to.</param>
        /// <param name="format">The format to save as. If <see cref="ImageFileFormat.__fromExtension"/>, the format
        /// will be determined from the filename by <see cref="Utilities.FormatFromExtension"/>.</param>
        /// <param name="t">A transformation to apply before saving or <c>null</c> for no transformation.</param>
        /// <returns></returns>
        public Task<bool> Save(string fname, ImageFileFormat? format=ImageFileFormat.__fromExtension, Transform t=null) {
            // TODO: Saving of FITS files and/or using Python
            return Task<bool>.Factory.StartNew(() => {
                if (format == ImageFileFormat.__fromExtension)
                    format = Utilities.FormatFromExtension(fname);
                if (format == null) return false;

                var encoder = (BitmapEncoder) Activator.CreateInstance(Encoders[(ImageFileFormat) format]);
                var img = t == null ? BitmapSource : new TransformedBitmap(BitmapSource, t);

                encoder.Frames.Add(BitmapFrame.Create(img));
                using (var fileStream = new FileStream(fname, FileMode.Create))
                    encoder.Save(fileStream);
                return true;
            });
        }

        private bool disposed;
        /// <summary>
        /// Dispose of the image by freeing the data buffer and disposing of the <see cref="rwlock"/>.
        /// </summary>
        /// <remarks>
        /// <para>Any subsequent data access operations will result in an error due to the inability to acquire the
        /// lock.</para>
        /// <para>This is automatically called in the destructor, so memory is automatically freed.</para>
        /// </remarks>
        public void Dispose() {
            if (disposed) return;
            rwlock.EnterWriteLock();
            Marshal.FreeHGlobal((IntPtr) raw);
            rwlock.ExitWriteLock();
            rwlock.Dispose();
            _bitmapCache = null;
            disposed = true;
        }

        ~CameraImage() {
            Dispose();
        }
    }
}
