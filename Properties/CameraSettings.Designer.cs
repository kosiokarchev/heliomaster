﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace heliomaster_wpf.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.5.0.0")]
    public sealed partial class CameraSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static CameraSettings defaultInstance = ((CameraSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new CameraSettings())));
        
        public static CameraSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfInt xmlns:xsi=\"http://www.w3.org" +
            "/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n  <int>" +
            "10</int>\r\n  <int>10</int>\r\n</ArrayOfInt>")]
        public int[] Gains {
            get {
                return ((int[])(this["Gains"]));
            }
            set {
                this["Gains"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfInt xmlns:xsi=\"http://www.w3.org" +
            "/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n  <int>" +
            "20</int>\r\n  <int>20</int>\r\n</ArrayOfInt>")]
        public int[] Exposures {
            get {
                return ((int[])(this["Exposures"]));
            }
            set {
                this["Exposures"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfCameraModel xmlns:xsi=\"http://ww" +
            "w.w3.org/2001/XMLSchema-instance\"\r\n                        xmlns:xsd=\"http://www" +
            ".w3.org/2001/XMLSchema\">\r\n                        <CameraModel>\r\n<CameraType>QHY" +
            "CCD</CameraType>\r\n                            <Index>0</Index>\r\n                " +
            "            <Name>Visible</Name>\r\n                            <LocalPathFormat>C" +
            ":\\\\Users\\\\Kosio\\\\Desktop\\\\test_out\\\\visible\\\\{DateTime:yyyyMMddTHHmmss}.png</Loc" +
            "alPathFormat>\r\n                            <RemotePathFormat>/home/sun_monitor/f" +
            "tp_public/ftp_test/image_helios_{Cam}_{DateTime:yyyyMMddTHHmmss}.bmp</RemotePath" +
            "Format>\r\n                            <RemoteCommandFormat>uptime 1&gt;&amp;2</Re" +
            "moteCommandFormat>\r\n                            <Flip>false</Flip>\r\n            " +
            "                <Rotate>0\r\n</Rotate>\r\n                        </CameraModel>\r\n  " +
            "                      <CameraModel>\r\n                            <Index>1</Index" +
            ">\r\n                            <Name>Halpha</Name>\r\n                            " +
            "<LocalPathFormat>C:\\\\Users\\\\Kosio\\\\Desktop\\\\test_out\\\\halpha\\\\{DateTime:yyyyMMdd" +
            "THHmmss}.png</LocalPathFormat>\r\n                            <RemotePathFormat>/h" +
            "ome/sun_monitor/ftp_public/ftp_test/image_helios_{Cam}_{DateTime:yyyyMMddTHHmmss" +
            "}.bmp</RemotePathFormat>\r\n                            <RemoteCommandFormat>pwd</" +
            "RemoteCommandFormat>\r\n                            <Flip>false</Flip>\r\n          " +
            "                  <Rotate>0</Rotate>\r\n                        </CameraModel>    " +
            "                </ArrayOfCameraModel>")]
        public global::System.Collections.ObjectModel.ObservableCollection<heliomaster_wpf.CameraModel> CameraModels {
            get {
                return ((global::System.Collections.ObjectModel.ObservableCollection<heliomaster_wpf.CameraModel>)(this["CameraModels"]));
            }
            set {
                this["CameraModels"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SaveTransformed {
            get {
                return ((bool)(this["SaveTransformed"]));
            }
            set {
                this["SaveTransformed"] = value;
            }
        }
    }
}
