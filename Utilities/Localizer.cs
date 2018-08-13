using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace heliomaster_wpf {
    public abstract class Localizer {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        protected static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private readonly IntPtr pDll;

        protected Localizer(string dllname, Type headerType) {
            pDll = LoadLibrary(dllname);
            if (pDll == IntPtr.Zero) throw new Exception($"Could not load {dllname}");

            Console.WriteLine($"Loaded {dllname} at {pDll}");

            var thisType = GetType();
            IntPtr addr;
            foreach (var m in headerType.GetMembers())
                if (m.CustomAttributes.Any(a => a.AttributeType == typeof(UnmanagedFunctionPointerAttribute)) &&
                    m.GetCustomAttribute(typeof(DescriptionAttribute)) is DescriptionAttribute da &&
                    da.Description is string Name && (addr = GetProcAddress(pDll, Name)) != IntPtr.Zero &&
                    thisType.GetProperty(m.Name) is PropertyInfo prop) {
                    prop.SetValue(this, Marshal.GetDelegateForFunctionPointer(addr, prop.PropertyType));
                    Console.WriteLine($"{m.Name} --> {addr}");
                }


        }

        ~Localizer() {
            if (pDll != IntPtr.Zero) FreeLibrary(pDll);
        }
    }

    public class QHYCCDLocalizer : Localizer {
        public QHYCCDLocalizer() : base(
            @"C:\Users\Kosio\cesar\heliomaster\heliomaster-wpf\bin\x86\Debug\qhyccd.dll",
            typeof(libQHYCCD)) {
            Console.WriteLine($"Loaded tbb at {LoadLibrary(@"C:\Users\Kosio\cesar\heliomaster\heliomaster-wpf\bin\x86\Debug\tbb.dll")}");
            Console.WriteLine($"Loaded ftd2xx at {LoadLibrary(@"C:\Users\Kosio\cesar\heliomaster\heliomaster-wpf\bin\x86\Debug\ftd2xx.dll")}");
            Console.WriteLine($"Init: {InitQHYCCDResource()}");
        }

        ~QHYCCDLocalizer() {
            ReleaseQHYCCDResource();
        }

        public enum CONTROL_ID {
            CONTROL_BRIGHTNESS = 0, //!< image brightness
            CONTROL_CONTRAST,       //!< image contrast
            CONTROL_WBR,            //!< red of white balance
            CONTROL_WBB,            //!< blue of white balance
            CONTROL_WBG,            //!< the green of white balance
            CONTROL_GAMMA,          //!< screen gamma
            CONTROL_GAIN,           //!< camera gain
            CONTROL_OFFSET,         //!< camera offset
            CONTROL_EXPOSURE,       //!< expose time (us)
            CONTROL_SPEED,          //!< transfer speed
            CONTROL_TRANSFERBIT,    //!< image depth bits
            CONTROL_CHANNELS,       //!< image channels
            CONTROL_USBTRAFFIC,     //!< hblank
            CONTROL_ROWNOISERE,     //!< row denoise
            CONTROL_CURTEMP,        //!< current cmos or ccd temprature
            CONTROL_CURPWM,         //!< current cool pwm
            CONTROL_MANULPWM,       //!< set the cool pwm
            CONTROL_CFWPORT,        //!< control camera color filter wheel port
            CONTROL_COOLER,         //!< check if camera has cooler
            CONTROL_ST4PORT,        //!< check if camera has st4port
            CAM_COLOR,
            CAM_BIN1X1MODE,                      //!< check if camera has bin1x1 mode
            CAM_BIN2X2MODE,                      //!< check if camera has bin2x2 mode
            CAM_BIN3X3MODE,                      //!< check if camera has bin3x3 mode
            CAM_BIN4X4MODE,                      //!< check if camera has bin4x4 mode
            CAM_MECHANICALSHUTTER,               //!< mechanical shutter
            CAM_TRIGER_INTERFACE,                //!< triger
            CAM_TECOVERPROTECT_INTERFACE,        //!< tec overprotect
            CAM_SINGNALCLAMP_INTERFACE,          //!< singnal clamp
            CAM_FINETONE_INTERFACE,              //!< fine tone
            CAM_SHUTTERMOTORHEATING_INTERFACE,   //!< shutter motor heating
            CAM_CALIBRATEFPN_INTERFACE,          //!< calibrated frame
            CAM_CHIPTEMPERATURESENSOR_INTERFACE, //!< chip temperaure sensor
            CAM_USBREADOUTSLOWEST_INTERFACE,     //!< usb readout slowest
            CAM_8BITS,                           //!< 8bit depth
            CAM_16BITS,                          //!< 16bit depth
            CAM_GPS,                             //!< check if camera has gps
            CAM_IGNOREOVERSCAN_INTERFACE,        //!< ignore overscan area
            QHYCCD_3A_AUTOBALANCE,
            QHYCCD_3A_AUTOEXPOSURE,
            QHYCCD_3A_AUTOFOCUS,
            CONTROL_AMPV, //!< ccd or cmos ampv
            CONTROL_VCAM, //!< Virtual Camera on off
            CAM_VIEW_MODE,
            CONTROL_CFWSLOTSNUM, //!< check CFW slots number
            IS_EXPOSING_DONE,
            ScreenStretchB,
            ScreenStretchW,
            CONTROL_DDR,
            CAM_LIGHT_PERFORMANCE_MODE,
            CAM_QHY5II_GUIDE_MODE,
            DDR_BUFFER_CAPACITY,
            DDR_BUFFER_READ_THRESHOLD
        };

        enum BAYER_ID {
            BAYER_GB = 1,
            BAYER_GR,
            BAYER_BG,
            BAYER_RG
        }

        public libQHYCCD.InitQHYCCDResource              InitQHYCCDResource              { get; set; }
        public libQHYCCD.ReleaseQHYCCDResource           ReleaseQHYCCDResource           { get; set; }
        public libQHYCCD.ScanQHYCCD                      ScanQHYCCD                      { get; set; }
        public libQHYCCD.GetQHYCCDId                     GetQHYCCDId                     { get; set; }
        public libQHYCCD.GetQHYCCDModel                  GetQHYCCDModel                  { get; set; }
        public libQHYCCD.OpenQHYCCD                      OpenQHYCCD                      { get; set; }
        public libQHYCCD.CloseQHYCCD                     CloseQHYCCD                     { get; set; }
        public libQHYCCD.SetQHYCCDStreamMode             SetQHYCCDStreamMode             { get; set; }
        public libQHYCCD.InitQHYCCD                      InitQHYCCD                      { get; set; }
        public libQHYCCD.IsQHYCCDControlAvailable        IsQHYCCDControlAvailable        { get; set; }
        public libQHYCCD.SetQHYCCDParam                  SetQHYCCDParam                  { get; set; }
        public libQHYCCD.GetQHYCCDParam                  GetQHYCCDParam                  { get; set; }
        public libQHYCCD.GetQHYCCDParamMinMaxStep        GetQHYCCDParamMinMaxStep        { get; set; }
        public libQHYCCD.SetQHYCCDResolution             SetQHYCCDResolution             { get; set; }
        public libQHYCCD.GetQHYCCDMemLength              GetQHYCCDMemLength              { get; set; }
        public libQHYCCD.ExpQHYCCDSingleFrame            ExpQHYCCDSingleFrame            { get; set; }
        public libQHYCCD.GetQHYCCDSingleFrame            GetQHYCCDSingleFrame            { get; set; }
        public libQHYCCD.CancelQHYCCDExposing            CancelQHYCCDExposing            { get; set; }
        public libQHYCCD.CancelQHYCCDExposingAndReadout  CancelQHYCCDExposingAndReadout  { get; set; }
        public libQHYCCD.BeginQHYCCDLive                 BeginQHYCCDLive                 { get; set; }
        public libQHYCCD.GetQHYCCDLiveFrame              GetQHYCCDLiveFrame              { get; set; }
        public libQHYCCD.StopQHYCCDLive                  StopQHYCCDLive                  { get; set; }
        public libQHYCCD.SetQHYCCDBinMode                SetQHYCCDBinMode                { get; set; }
        public libQHYCCD.SetQHYCCDBitsMode               SetQHYCCDBitsMode               { get; set; }
        public libQHYCCD.ControlQHYCCDTemp               ControlQHYCCDTemp               { get; set; }
        public libQHYCCD.ControlQHYCCDGuide              ControlQHYCCDGuide              { get; set; }
        public libQHYCCD.SendOrder2QHYCCDCFW             SendOrder2QHYCCDCFW             { get; set; }
        public libQHYCCD.GetQHYCCDCFWStatus              GetQHYCCDCFWStatus              { get; set; }
        public libQHYCCD.IsQHYCCDCFWPlugged              IsQHYCCDCFWPlugged              { get; set; }
        public libQHYCCD.SetQHYCCDTrigerMode             SetQHYCCDTrigerMode             { get; set; }
        public libQHYCCD.Bits16ToBits8                   Bits16ToBits8                   { get; set; }
        public libQHYCCD.HistInfo192x130                 HistInfo192x130                 { get; set; }
        public libQHYCCD.GetQHYCCDChipInfo               GetQHYCCDChipInfo               { get; set; }
        public libQHYCCD.GetQHYCCDEffectiveArea          GetQHYCCDEffectiveArea          { get; set; }
        public libQHYCCD.GetQHYCCDOverScanArea           GetQHYCCDOverScanArea           { get; set; }
        public libQHYCCD.SetQHYCCDFocusSetting           SetQHYCCDFocusSetting           { get; set; }
        public libQHYCCD.GetQHYCCDExposureRemaining      GetQHYCCDExposureRemaining      { get; set; }
        public libQHYCCD.GetQHYCCDFWVersion              GetQHYCCDFWVersion              { get; set; }
        public libQHYCCD.SetQHYCCDInterCamSerialParam    SetQHYCCDInterCamSerialParam    { get; set; }
        public libQHYCCD.QHYCCDInterCamSerialTX          QHYCCDInterCamSerialTX          { get; set; }
        public libQHYCCD.QHYCCDInterCamSerialRX          QHYCCDInterCamSerialRX          { get; set; }
        public libQHYCCD.QHYCCDInterCamOledOnOff         QHYCCDInterCamOledOnOff         { get; set; }
        public libQHYCCD.SetQHYCCDInterCamOledBrightness SetQHYCCDInterCamOledBrightness { get; set; }
        public libQHYCCD.SendFourLine2QHYCCDInterCamOled SendFourLine2QHYCCDInterCamOled { get; set; }
        public libQHYCCD.SendTwoLine2QHYCCDInterCamOled  SendTwoLine2QHYCCDInterCamOled  { get; set; }
        public libQHYCCD.SendOneLine2QHYCCDInterCamOled  SendOneLine2QHYCCDInterCamOled  { get; set; }
        public libQHYCCD.GetQHYCCDCameraStatus           GetQHYCCDCameraStatus           { get; set; }
        public libQHYCCD.GetQHYCCDShutterStatus          GetQHYCCDShutterStatus          { get; set; }
        public libQHYCCD.ControlQHYCCDShutter            ControlQHYCCDShutter            { get; set; }
        public libQHYCCD.GetQHYCCDHumidity               GetQHYCCDHumidity               { get; set; }
        public libQHYCCD.QHYCCDI2CTwoWrite               QHYCCDI2CTwoWrite               { get; set; }
        public libQHYCCD.QHYCCDI2CTwoRead                QHYCCDI2CTwoRead                { get; set; }
        public libQHYCCD.GetQHYCCDReadingProgress        GetQHYCCDReadingProgress        { get; set; }
        public libQHYCCD.SetQHYCCDLogLevel               SetQHYCCDLogLevel               { get; set; }
        public libQHYCCD.TestQHYCCDPIDParas              TestQHYCCDPIDParas              { get; set; }
        public libQHYCCD.SetQHYCCDTrigerFunction         SetQHYCCDTrigerFunction         { get; set; }
        public libQHYCCD.DownloadFX3FirmWare             DownloadFX3FirmWare             { get; set; }
        public libQHYCCD.GetQHYCCDType                   GetQHYCCDType                   { get; set; }
        public libQHYCCD.SetQHYCCDDebayerOnOff           SetQHYCCDDebayerOnOff           { get; set; }
        public libQHYCCD.SetQHYCCDFineTone               SetQHYCCDFineTone               { get; set; }
        public libQHYCCD.SetQHYCCDGPSVCOXFreq            SetQHYCCDGPSVCOXFreq            { get; set; }
        public libQHYCCD.SetQHYCCDGPSLedCalMode          SetQHYCCDGPSLedCalMode          { get; set; }
        public libQHYCCD.SetQHYCCDGPSLedCal              SetQHYCCDGPSLedCal              { get; set; }
        public libQHYCCD.SetQHYCCDGPSPOSA                SetQHYCCDGPSPOSA                { get; set; }
        public libQHYCCD.SetQHYCCDGPSPOSB                SetQHYCCDGPSPOSB                { get; set; }
        public libQHYCCD.SetQHYCCDGPSMasterSlave         SetQHYCCDGPSMasterSlave         { get; set; }
        public libQHYCCD.SetQHYCCDGPSSlaveModeParameter  SetQHYCCDGPSSlaveModeParameter  { get; set; }
        public libQHYCCD.QHYCCDVendRequestWrite          QHYCCDVendRequestWrite          { get; set; }
        public libQHYCCD.QHYCCDReadUSB_SYNC              QHYCCDReadUSB_SYNC              { get; set; }
        public libQHYCCD.QHYCCDLibusbBulkTransfer        QHYCCDLibusbBulkTransfer        { get; set; }
        public libQHYCCD.GetQHYCCDSDKVersion             GetQHYCCDSDKVersion             { get; set; }
    }

    public unsafe class libQHYCCD {
        [Description("_BeginQHYCCDLive@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 BeginQHYCCDLive(IntPtr handle);

        [Description("_Bits16ToBits8@28")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void Bits16ToBits8(IntPtr h, byte* InputData16, byte* OutputData8, UInt32 imageX, UInt32 imageY, UInt16 B, UInt16 W);

        [Description("_CancelQHYCCDExposing@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 CancelQHYCCDExposing(IntPtr handle);

        [Description("_CancelQHYCCDExposingAndReadout@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 CancelQHYCCDExposingAndReadout(IntPtr handle);

        [Description("_CloseQHYCCD@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 CloseQHYCCD(IntPtr handle);

        [Description("_ControlQHYCCDGuide@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ControlQHYCCDGuide(IntPtr handle, UInt32 direction, UInt16 duration);

        [Description("_ControlQHYCCDShutter@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ControlQHYCCDShutter(IntPtr handle, byte status);

        [Description("_ControlQHYCCDTemp@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ControlQHYCCDTemp(IntPtr handle, double targettemp);

        [Description("_DownloadFX3FirmWare@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 DownloadFX3FirmWare(UInt16 vid, UInt16 pid, StringBuilder imgpath);

        [Description("_ExpQHYCCDSingleFrame@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ExpQHYCCDSingleFrame(IntPtr handle);

        [Description("_GetQHYCCDCameraStatus@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDCameraStatus(IntPtr h, byte* buf);

        [Description("_GetQHYCCDCFWStatus@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDCFWStatus(IntPtr handle, StringBuilder status);

        [Description("_GetQHYCCDChipInfo@32")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDChipInfo(IntPtr h, ref double chipw, ref double chiph, ref UInt32 imagew, ref UInt32 imageh, ref double pixelw, ref double pixelh, ref UInt32 bpp);

        [Description("_GetQHYCCDEffectiveArea@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDEffectiveArea(IntPtr h, ref UInt32 startX, ref UInt32 startY, ref UInt32 sizeX, ref UInt32 sizeY);

        [Description("_GetQHYCCDExposureRemaining@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDExposureRemaining(IntPtr h);

        [Description("_GetQHYCCDFWVersion@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDFWVersion(IntPtr h, byte* buf);

        [Description("_GetQHYCCDHumidity@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDHumidity(IntPtr handle, ref double hd);

        [Description("_GetQHYCCDId@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDId(UInt32 index, StringBuilder id);

        [Description("_GetQHYCCDLiveFrame@24")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDLiveFrame(IntPtr handle, ref UInt32 w, ref UInt32 h, ref UInt32 bpp, ref UInt32 channels, byte* imgdata);

        [Description("_GetQHYCCDMemLength@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDMemLength(IntPtr handle);

        [Description("_GetQHYCCDModel@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDModel(StringBuilder id, StringBuilder model);

        [Description("_GetQHYCCDOverScanArea@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDOverScanArea(IntPtr h, ref UInt32 startX, ref UInt32 startY, ref UInt32 sizeX, ref UInt32 sizeY);

        [Description("_GetQHYCCDParam@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate double GetQHYCCDParam(IntPtr handle, QHYCCDLocalizer.CONTROL_ID controlId);

        [Description("_GetQHYCCDParamMinMaxStep@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDParamMinMaxStep(IntPtr handle, QHYCCDLocalizer.CONTROL_ID controlId, ref double min, ref double max, ref double step);

        [Description("_GetQHYCCDReadingProgress@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate double GetQHYCCDReadingProgress(IntPtr handle);

        [Description("_GetQHYCCDSDKVersion@16")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDSDKVersion(ref UInt32 year, ref UInt32 month, ref UInt32 day, ref UInt32 subday);

        [Description("_GetQHYCCDShutterStatus@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDShutterStatus(IntPtr handle);

        [Description("_GetQHYCCDSingleFrame@24")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDSingleFrame(IntPtr handle, ref UInt32 w, ref UInt32 h, ref UInt32 bpp, ref UInt32 channels, byte* imgdata);

        [Description("_GetQHYCCDType@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 GetQHYCCDType(IntPtr h);

        [Description("_HistInfo192x130@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void HistInfo192x130(IntPtr h, UInt32 x, UInt32 y, byte* InBuf, byte* OutBuf);

        [Description("_InitQHYCCD@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 InitQHYCCD(IntPtr handle);

        [Description("_InitQHYCCDResource@0")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 InitQHYCCDResource();

        [Description("_IsQHYCCDCFWPlugged@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 IsQHYCCDCFWPlugged(IntPtr handle);

        [Description("_IsQHYCCDControlAvailable@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 IsQHYCCDControlAvailable(IntPtr handle, QHYCCDLocalizer.CONTROL_ID controlId);

        [Description("_OpenQHYCCD@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Unicode, ThrowOnUnmappableChar = true)]
        public delegate IntPtr OpenQHYCCD(StringBuilder id);

        [Description("_QHYCCDI2CTwoRead@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDI2CTwoRead(IntPtr handle, UInt16 addr);

        [Description("_QHYCCDI2CTwoWrite@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDI2CTwoWrite(IntPtr handle, UInt16 addr, UInt16 value);

        [Description("_QHYCCDInterCamOledOnOff@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDInterCamOledOnOff(IntPtr handle, byte onoff);

        [Description("_QHYCCDInterCamSerialRX@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDInterCamSerialRX(IntPtr h, StringBuilder buf);

        [Description("_QHYCCDInterCamSerialTX@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDInterCamSerialTX(IntPtr h, StringBuilder buf, UInt32 length);

        [Description("_QHYCCDLibusbBulkTransfer@24")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDLibusbBulkTransfer(IntPtr pDevHandle, byte endpoint, byte* data, UInt32 length, ref Int32 transferred, UInt32 timeout);

        [Description("_QHYCCDReadUSB_SYNC@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDReadUSB_SYNC(IntPtr pDevHandle, byte endpoint, UInt32 length, byte* data, UInt32 timeout);

        [Description("_QHYCCDVendRequestWrite@24")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 QHYCCDVendRequestWrite(IntPtr h, byte req, UInt16 value, UInt16 index1, UInt32 length, byte* data);

        [Description("_ReleaseQHYCCDResource@0")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ReleaseQHYCCDResource();

        [Description("_ScanQHYCCD@0")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 ScanQHYCCD();

        [Description("_SendFourLine2QHYCCDInterCamOled@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SendFourLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messagetemp, StringBuilder messageinfo, StringBuilder messagetime, StringBuilder messagemode);

        [Description("_SendOneLine2QHYCCDInterCamOled@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SendOneLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messageTop);

        [Description("_SendOrder2QHYCCDCFW@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SendOrder2QHYCCDCFW(IntPtr handle, StringBuilder order, UInt32 length);

        [Description("_SendTwoLine2QHYCCDInterCamOled@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SendTwoLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messageTop, StringBuilder messageBottom);

        [Description("_SetQHYCCDBinMode@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDBinMode(IntPtr handle, UInt32 wbin, UInt32 hbin);

        [Description("_SetQHYCCDBitsMode@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDBitsMode(IntPtr handle, UInt32 bits);

        [Description("_SetQHYCCDDebayerOnOff@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDDebayerOnOff(IntPtr h, bool onoff);

        [Description("_SetQHYCCDFineTone@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDFineTone(IntPtr h, byte setshporshd, byte shdloc, byte shploc, byte shwidth);

        [Description("_SetQHYCCDFocusSetting@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDFocusSetting(IntPtr h, UInt32 focusCenterX, UInt32 focusCenterY);

        [Description("_SetQHYCCDGPSLedCal@12")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void SetQHYCCDGPSLedCal(IntPtr handle, UInt32 pos, byte width);

        [Description("_SetQHYCCDGPSLedCalMode@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDGPSLedCalMode(IntPtr handle, byte i);

        [Description("_SetQHYCCDGPSMasterSlave@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDGPSMasterSlave(IntPtr handle, byte i);

        [Description("_SetQHYCCDGPSPOSA@16")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void SetQHYCCDGPSPOSA(IntPtr handle, byte is_slave, UInt32 pos, byte width);

        [Description("_SetQHYCCDGPSPOSB@16")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void SetQHYCCDGPSPOSB(IntPtr handle, byte is_slave, UInt32 pos, byte width);

        [Description("_SetQHYCCDGPSSlaveModeParameter@24")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void SetQHYCCDGPSSlaveModeParameter(IntPtr handle, UInt32 target_sec, UInt32 target_us, UInt32 deltaT_sec, UInt32 deltaT_us, UInt32 expTime);

        [Description("_SetQHYCCDGPSVCOXFreq@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDGPSVCOXFreq(IntPtr handle, UInt16 i);

        [Description("_SetQHYCCDInterCamOledBrightness@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDInterCamOledBrightness(IntPtr handle, byte brightness);

        [Description("_SetQHYCCDInterCamSerialParam@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDInterCamSerialParam(IntPtr h, UInt32 opt);

        [Description("_SetQHYCCDLogLevel@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate void SetQHYCCDLogLevel(byte logLevel);

        [Description("_SetQHYCCDParam@16")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDParam(IntPtr handle, QHYCCDLocalizer.CONTROL_ID controlId, double value);

        [Description("_SetQHYCCDResolution@20")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDResolution(IntPtr handle, UInt32 x, UInt32 y, UInt32 xsize, UInt32 ysize);

        [Description("_SetQHYCCDStreamMode@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDStreamMode(IntPtr handle, byte mode);

        [Description("_SetQHYCCDTrigerFunction@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDTrigerFunction(IntPtr h, bool value);

        [Description("_SetQHYCCDTrigerMode@8")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 SetQHYCCDTrigerMode(IntPtr handle, UInt32 trigerMode);

        [Description("_StopQHYCCDLive@4")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 StopQHYCCDLive(IntPtr handle);

        [Description("_TestQHYCCDPIDParas@28")]
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet=CharSet.Ansi)]
        public delegate UInt32 TestQHYCCDPIDParas(IntPtr h, double p, double i, double d);
    }
}
