using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace heliomaster {
     public enum BitDepth {
        depth8,
        depth16,
        depth32
    }

    public unsafe class CameraImage {
        public enum ImageFileFormat {
            __fromExtension,
            png, jpeg, tiff,
        }
        private static readonly Dictionary<ImageFileFormat, Type> Encoders = new Dictionary<ImageFileFormat, Type> {
            {ImageFileFormat.png, typeof(PngBitmapEncoder)},
            {ImageFileFormat.jpeg, typeof(JpegBitmapEncoder)},
            {ImageFileFormat.tiff, typeof(TiffBitmapEncoder)},
        };

        public readonly void* raw;

        public readonly int Width;
        public readonly int Height;
        public readonly int Channels;
        public readonly BitDepth Depth;
        public int Length => Width * Height * Channels;
        public int Size => Length * BaseCamera.DepthSizes[Depth];

        protected CameraImage(int width, int height, int channels, BitDepth depth) {
            Width    = width;
            Height   = height;
            Channels = channels;
            Depth    = depth;
            raw      = (void*) Marshal.AllocHGlobal(Size);
        }

        public readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();

        public void BufferUpdated() {
            _bitmapCache = null;
        }

        public virtual void Put(Array data) {
            rwlock.EnterWriteLock();

            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);
            unsafe {
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
            }
            gch.Free();

            rwlock.ExitWriteLock();

            BufferUpdated();
        }

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
                stride: Width * Channels * BaseCamera.DepthSizes[Depth]);
            img.Freeze();
            rwlock.ExitReadLock();
            return img;
        }

        private BitmapSource _bitmapCache;
        public BitmapSource BitmapSource => _bitmapCache ?? (_bitmapCache = GetBitmap());

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


        public Task<bool> Save(string fname, ImageFileFormat? fmt=ImageFileFormat.__fromExtension, Transform t=null) {
            return Task<bool>.Factory.StartNew(() => {
                if (fmt == ImageFileFormat.__fromExtension)
                    fmt = Utilities.FormatFromExtension(fname);
                if (fmt == null) return false;

                var encoder = (BitmapEncoder) Activator.CreateInstance(Encoders[(ImageFileFormat) fmt]);
                var img = t == null ? BitmapSource : new TransformedBitmap(BitmapSource, t);

                encoder.Frames.Add(BitmapFrame.Create(img));
                using (var fileStream = new FileStream(fname, FileMode.Create))
                    encoder.Save(fileStream);
                return true;
            });
        }

        ~CameraImage() {
            rwlock.EnterWriteLock();
            Marshal.FreeHGlobal((IntPtr) raw);
            rwlock.ExitWriteLock();
            rwlock.Dispose();
        }
    }
}
