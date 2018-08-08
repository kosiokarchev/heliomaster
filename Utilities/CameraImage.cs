﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace heliomaster_wpf {
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

        protected readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();

        public virtual void Put(Array data) {
            _bitmapCache = null;
        }

        public CameraImage Copy() {
            var ret = new CameraImage(Width, Height, Channels, Depth);

            rwlock.EnterReadLock();

            ret.rwlock.EnterWriteLock();
            for (var i = 0; i < Size; ++i)
                ret.raw[i] = raw[i];
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


        public Task<bool> Save(string fname, ImageFileFormat? fmt=ImageFileFormat.__fromExtension) {
            return Task<bool>.Factory.StartNew(() => {
                if (fmt == ImageFileFormat.__fromExtension)
                    fmt = Utilities.FormatFromExtension(fname);
                if (fmt == null) return false;

                var encoder = (BitmapEncoder) Activator.CreateInstance(Encoders[(ImageFileFormat) fmt]);
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource));
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

    public sealed class ASCOMImage : CameraImage {
        public ASCOMImage(Array data, int channels, BitDepth depth) : base(
            data.GetLength(0), data.GetLength(1), channels, depth) {
            Put(data);
        }

        public override void Put(Array data) {
            base.Put(data);

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
        }
    }
}
