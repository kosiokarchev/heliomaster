using System;
using System.Runtime.InteropServices;
using System.Text;

namespace heliomaster_wpf {
 public static class libqhyccd {
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

  public enum BAYER_ID {
   BAYER_GB = 1,
   BAYER_GR,
   BAYER_BG,
   BAYER_RG
  }

  [DllImport("qhyccd.dll", EntryPoint = "InitQHYCCDResource", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 InitQHYCCDResource();

  [DllImport("qhyccd.dll", EntryPoint = "ReleaseQHYCCDResource", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ReleaseQHYCCDResource();

  [DllImport("qhyccd.dll", EntryPoint = "ScanQHYCCD", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ScanQHYCCD();

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDId", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDId(UInt32 index, StringBuilder id);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDModel", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDModel(StringBuilder id, StringBuilder model);

  [DllImport("qhyccd.dll", EntryPoint = "OpenQHYCCD", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern IntPtr OpenQHYCCD(StringBuilder id);

  [DllImport("qhyccd.dll", EntryPoint = "CloseQHYCCD", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 CloseQHYCCD(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDStreamMode", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDStreamMode(IntPtr handle, byte mode);

  [DllImport("qhyccd.dll", EntryPoint = "InitQHYCCD", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 InitQHYCCD(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "IsQHYCCDControlAvailable", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 IsQHYCCDControlAvailable(IntPtr handle, CONTROL_ID controlId);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDParam", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDParam(IntPtr handle, CONTROL_ID controlId, double value);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDParam", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern double GetQHYCCDParam(IntPtr handle, CONTROL_ID controlId);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDParamMinMaxStep", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDParamMinMaxStep(IntPtr handle, CONTROL_ID controlId, ref double min,
                                                       ref double max, ref double step);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDResolution", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDResolution(IntPtr handle, UInt32 x, UInt32 y, UInt32 xsize, UInt32 ysize);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDMemLength", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDMemLength(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "ExpQHYCCDSingleFrame", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ExpQHYCCDSingleFrame(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDSingleFrame", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 GetQHYCCDSingleFrame(IntPtr handle, ref UInt32 w, ref UInt32 h,
                                                          ref UInt32 bpp,
                                                          ref UInt32 channels, byte * imgdata);

  [DllImport("qhyccd.dll", EntryPoint = "CancelQHYCCDExposing", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 CancelQHYCCDExposing(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "CancelQHYCCDExposingAndReadout", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 CancelQHYCCDExposingAndReadout(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "BeginQHYCCDLive", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 BeginQHYCCDLive(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDLiveFrame", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 GetQHYCCDLiveFrame(IntPtr handle, ref UInt32 w, ref UInt32 h, ref UInt32 bpp,
                                                        ref UInt32 channels, byte * imgdata);

  [DllImport("qhyccd.dll", EntryPoint = "StopQHYCCDLive", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 StopQHYCCDLive(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDBinMode", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDBinMode(IntPtr handle, UInt32 wbin, UInt32 hbin);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDBitsMode", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDBitsMode(IntPtr handle, UInt32 bits);

  [DllImport("qhyccd.dll", EntryPoint = "ControlQHYCCDTemp", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ControlQHYCCDTemp(IntPtr handle, double targettemp);

  [DllImport("qhyccd.dll", EntryPoint = "ControlQHYCCDGuide", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ControlQHYCCDGuide(IntPtr handle, UInt32 direction, UInt16 duration);

  [DllImport("qhyccd.dll", EntryPoint = "SendOrder2QHYCCDCFW", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SendOrder2QHYCCDCFW(IntPtr handle, StringBuilder order, UInt32 length);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDCFWStatus", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDCFWStatus(IntPtr handle, StringBuilder status);

  [DllImport("qhyccd.dll", EntryPoint = "IsQHYCCDCFWPlugged", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 IsQHYCCDCFWPlugged(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDTrigerMode", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDTrigerMode(IntPtr handle, UInt32 trigerMode);

  [DllImport("qhyccd.dll", EntryPoint = "Bits16ToBits8", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe void Bits16ToBits8(IntPtr h, byte * InputData16, byte * OutputData8, UInt32 imageX,
                                                 UInt32 imageY, UInt16 B, UInt16 W);

  [DllImport("qhyccd.dll", EntryPoint = "HistInfo192x130", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe void HistInfo192x130(IntPtr h, UInt32 x, UInt32 y, byte * InBuf, byte * OutBuf);

  [DllImport("qhyccd.dll", EntryPoint = "OSXInitQHYCCDFirmware", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 OSXInitQHYCCDFirmware(StringBuilder path);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDChipInfo", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDChipInfo(IntPtr h, ref double chipw, ref double chiph,
                                                ref UInt32 imagew,
                                                ref UInt32 imageh, ref double pixelw, ref double pixelh,
                                                ref UInt32 bpp);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDEffectiveArea", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDEffectiveArea(IntPtr h, ref UInt32 startX, ref UInt32 startY,
                                                     ref UInt32 sizeX,
                                                     ref UInt32 sizeY);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDOverScanArea", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDOverScanArea(IntPtr h, ref UInt32 startX, ref UInt32 startY,
                                                    ref UInt32 sizeX,
                                                    ref UInt32 sizeY);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDFocusSetting", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDFocusSetting(IntPtr h, UInt32 focusCenterX, UInt32 focusCenterY);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDExposureRemaining", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDExposureRemaining(IntPtr h);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDFWVersion", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 GetQHYCCDFWVersion(IntPtr h, byte * buf);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDInterCamSerialParam", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDInterCamSerialParam(IntPtr h, UInt32 opt);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDInterCamSerialTX", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 QHYCCDInterCamSerialTX(IntPtr h, StringBuilder buf, UInt32 length);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDInterCamSerialRX", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 QHYCCDInterCamSerialRX(IntPtr h, StringBuilder buf);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDInterCamOledOnOff", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 QHYCCDInterCamOledOnOff(IntPtr handle, byte onoff);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDInterCamOledBrightness", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDInterCamOledBrightness(IntPtr handle, byte brightness);

  [DllImport("qhyccd.dll", EntryPoint = "SendFourLine2QHYCCDInterCamOled", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SendFourLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messagetemp,
                                                              StringBuilder messageinfo, StringBuilder messagetime,
                                                              StringBuilder messagemode);

  [DllImport("qhyccd.dll", EntryPoint = "SendTwoLine2QHYCCDInterCamOled", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SendTwoLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messageTop,
                                                             StringBuilder messageBottom);

  [DllImport("qhyccd.dll", EntryPoint = "SendOneLine2QHYCCDInterCamOled", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SendOneLine2QHYCCDInterCamOled(IntPtr handle, StringBuilder messageTop);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDCameraStatus", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 GetQHYCCDCameraStatus(IntPtr h, byte * buf);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDShutterStatus", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDShutterStatus(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "ControlQHYCCDShutter", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 ControlQHYCCDShutter(IntPtr handle, byte status);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDHumidity", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDHumidity(IntPtr handle, ref double hd);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDI2CTwoWrite", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 QHYCCDI2CTwoWrite(IntPtr handle, UInt16 addr, UInt16 value);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDI2CTwoRead", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 QHYCCDI2CTwoRead(IntPtr handle, UInt16 addr);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDReadingProgress", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern double GetQHYCCDReadingProgress(IntPtr handle);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDLogLevel", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern void SetQHYCCDLogLevel(byte logLevel);

  [DllImport("qhyccd.dll", EntryPoint = "TestQHYCCDPIDParas", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 TestQHYCCDPIDParas(IntPtr h, double p, double i, double d);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDTrigerFunction", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDTrigerFunction(IntPtr h, bool value);

  [DllImport("qhyccd.dll", EntryPoint = "DownloadFX3FirmWare", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 DownloadFX3FirmWare(UInt16 vid, UInt16 pid, StringBuilder imgpath);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDType", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDType(IntPtr h);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDDebayerOnOff", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDDebayerOnOff(IntPtr h, bool onoff);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDFineTone", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDFineTone(IntPtr h, byte setshporshd, byte shdloc, byte shploc, byte shwidth);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSVCOXFreq", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDGPSVCOXFreq(IntPtr handle, UInt16 i);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSLedCalMode", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDGPSLedCalMode(IntPtr handle, byte i);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSLedCal", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern void SetQHYCCDGPSLedCal(IntPtr handle, UInt32 pos, byte width);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSPOSA", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern void SetQHYCCDGPSPOSA(IntPtr handle, byte is_slave, UInt32 pos, byte width);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSPOSB", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern void SetQHYCCDGPSPOSB(IntPtr handle, byte is_slave, UInt32 pos, byte width);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSMasterSlave", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 SetQHYCCDGPSMasterSlave(IntPtr handle, byte i);

  [DllImport("qhyccd.dll", EntryPoint = "SetQHYCCDGPSSlaveModeParameter", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern void SetQHYCCDGPSSlaveModeParameter(IntPtr handle, UInt32 target_sec, UInt32 target_us,
                                                           UInt32 deltaT_sec, UInt32 deltaT_us, UInt32 expTime);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDVendRequestWrite", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 QHYCCDVendRequestWrite(IntPtr h, byte req, UInt16 value, UInt16 index1,
                                                            UInt32 length, byte * data);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDReadUSB_SYNC", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 QHYCCDReadUSB_SYNC(IntPtr pDevHandle, byte endpoint, UInt32 length, byte * data,
                                                        UInt32 timeout);

  [DllImport("qhyccd.dll", EntryPoint = "QHYCCDLibusbBulkTransfer", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern unsafe UInt32 QHYCCDLibusbBulkTransfer(IntPtr pDevHandle, byte endpoint, byte * data,
                                                              UInt32 length, ref Int32 transferred, UInt32 timeout);

  [DllImport("qhyccd.dll", EntryPoint = "GetQHYCCDSDKVersion", CharSet = CharSet.Ansi,
   CallingConvention                  = CallingConvention.StdCall)]
  public static extern UInt32 GetQHYCCDSDKVersion(ref UInt32 year, ref UInt32 month, ref UInt32 day, ref UInt32 subday);
 }
}
